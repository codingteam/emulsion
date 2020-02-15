module Emulsion.Tests.Telegram.Client

open System

open Funogram.Telegram.Types
open Xunit

open Emulsion
open Emulsion.Telegram
open Emulsion.Telegram.Funogram

let private selfUserId = 100500L
let private groupId = 200600L

let private createUser username firstName lastName = {
    Id = 0L
    FirstName = firstName
    LastName = lastName
    Username = username
    LanguageCode = None
    IsBot = false
}

let private currentChat = {
    defaultChat with
        Id = groupId
}

let private createMessage from text : Funogram.Telegram.Types.Message =
    { defaultMessage with
        From = from
        Chat = currentChat
        Text = text }

let private createReplyMessage from text replyTo : Funogram.Telegram.Types.Message =
    { createMessage from text with
        ReplyToMessage = (Some replyTo) }

let private createForwardedMessage from (forwarded: Funogram.Telegram.Types.Message) =
    { defaultMessage with
        From = Some from
        Chat = currentChat
        ForwardFrom = forwarded.From
        Text = forwarded.Text }

let private createStickerMessage from emoji =
    { defaultMessage with
        From = Some from
        Chat = currentChat
        Sticker = Some {
            FileId = ""
            Width = 0
            Height = 0
            Thumb = None
            FileSize = None
            Emoji = emoji
            SetName = None
            MaskPosition = None
            IsAnimated = false
        }
    }

let private createMessageWithCaption from caption =
    { defaultMessage with
        From = Some from
        Chat = currentChat
        Caption = Some caption }

let private telegramMessage author text =
    { main = { author = author; text = text }; replyTo = None }

let private telegramReplyMessage author text replyTo =
    { main = { author = author; text = text }; replyTo = Some replyTo }

let private createEntity t offset length url = {
    Type = t
    Offset = offset
    Length = length
    Url = Some url
    User = None
}

let private createEntities t offset length url = Some <| seq {
    createEntity t offset length url
}

let private originalUser = createUser (Some "originalUser") "" None
let private replyingUser = createUser (Some "replyingUser") "" None
let private forwardingUser = createUser (Some "forwardingUser") "" None

module ReadMessageTests =
    let readMessage = MessageConverter.read selfUserId

    [<Fact>]
    let readMessageWithUnknownUser() =
        Assert.Equal(
            telegramMessage "[UNKNOWN USER]" "",
            readMessage (createMessage None (Some ""))
        )

    [<Fact>]
    let readMessageWithKnownUsername() =
        let user = createUser (Some "Username") "" None
        Assert.Equal(
            telegramMessage "@Username" "",
            readMessage (createMessage (Some user) (Some ""))
        )

    [<Fact>]
    let readMessageWithFullName() =
        let user = createUser None "FirstName" (Some "LastName")
        Assert.Equal(
            telegramMessage "FirstName LastName" "",
            readMessage (createMessage (Some user) (Some ""))
        )

    [<Fact>]
    let readMessageWithFirstNameOnly() =
        let user = createUser None "FirstName" None
        Assert.Equal(
            telegramMessage "FirstName" "",
            readMessage (createMessage (Some user) (Some ""))
        )

    [<Fact>]
    let readMessageWithText() =
        Assert.Equal(
            telegramMessage "[UNKNOWN USER]" "text",
            readMessage (createMessage None (Some "text"))
        )

    [<Fact>]
    let readMessageWithoutText() =
        Assert.Equal(
            telegramMessage "[UNKNOWN USER]" "[DATA UNRECOGNIZED]",
            readMessage (createMessage None None)
        )

    [<Fact>]
    let readReplyMessage() =
        let originalMessage = createMessage (Some originalUser) (Some "Original text")
        let replyMessage = createReplyMessage (Some replyingUser) (Some "Reply text") originalMessage

        Assert.Equal(
            { main = { author = "@replyingUser"; text = "Reply text" }
              replyTo = Some { author = "@originalUser"; text = "Original text" } },
            readMessage replyMessage
        )

    [<Fact>]
    let readTextLinkMessage() =
        let message = { createMessage (Some originalUser) (Some "Original text") with
                            Entities = createEntities "text_link" 0L 8L "https://example.com" }

        Assert.Equal(
            telegramMessage "@originalUser" "Original [https://example.com] text",
            readMessage message
        )

    [<Fact>]
    let readMultipleTextLinksMessage() =
        let message = { createMessage (Some originalUser) (Some "Original text") with
                            Entities = Some <| upcast [| createEntity "text_link" 0L 8L "https://example.com/1"
                                                         createEntity "text_link" 9L 4L "https://example.com/2" |] }
        Assert.Equal(
            telegramMessage "@originalUser" "Original [https://example.com/1] text [https://example.com/2]",
            readMessage message
        )

    [<Fact>]
    let readInvalidTextLinkMessage() =
        let message = { createMessage (Some originalUser) (Some "Original text") with
                            Entities = createEntities "text_link" 0L 800L "https://example.com" }
        Assert.Equal(
            telegramMessage "@originalUser" "Original text [https://example.com]",
            readMessage message
        )

    [<Fact>]
    let readNegativeOffsetTextLinkMessage() =
        let message = { createMessage (Some originalUser) (Some "Original text") with
                            Entities = createEntities "text_link" -1L 5L "https://example.com" }
        Assert.Equal(
            telegramMessage "@originalUser" "Original text",
            readMessage message
        )

    [<Fact>]
    let readZeroLengthTextLinkMessage() =
        let message = { createMessage (Some originalUser) (Some "Original text") with
                            Entities = createEntities "text_link" 0L 0L "https://example.com" }
        Assert.Equal(
            telegramMessage "@originalUser" "Original text",
            readMessage message
        )

    [<Fact>]
    let readSuperLongLengthTextLinkMessage() =
        let message = { createMessage (Some originalUser) (Some "Original text") with
                            Entities = createEntities "text_link" Int64.MaxValue Int64.MaxValue "https://example.com" }
        Assert.Equal(
            telegramMessage "@originalUser" "Original text",
            readMessage message
        )

    [<Fact>]
    let readOverlappingTextLinksMessage() =
        let message = { createMessage (Some originalUser) (Some "Original text") with
                            Entities = Some <| upcast [| createEntity "text_link" 0L 8L "https://example.com/1"
                                                         createEntity "text_link" 0L 13L "https://example.com/2" |] }
        Assert.Equal(
            telegramMessage "@originalUser" "Original [https://example.com/1] text [https://example.com/2]",
            readMessage message
        )

    [<Fact>]
    let readNonTextLinkMessage() =
        let message = { createMessage (Some originalUser) (Some "Original text") with
                            Entities = createEntities "not_link" 0L 0L "https://example.com" }
        Assert.Equal(
            telegramMessage "@originalUser" "Original text",
            readMessage message
        )

    [<Fact>]
    let readNoneTextLinkMessage() =
        let message = { createMessage (Some originalUser) (Some "Original text") with
                            Entities = Some <| upcast [| { Type = "text_link"
                                                           Url = None
                                                           Offset = 0L
                                                           Length = 5L
                                                           User = None } |] }
        Assert.Equal(
            telegramMessage "@originalUser" "Original text",
            readMessage message
        )

    [<Fact>]
    let forward(): unit =
        let forwardedMessage = createMessage (Some originalUser) (Some "test")
        let message = createForwardedMessage forwardingUser forwardedMessage

        Assert.Equal(
            telegramMessage "@forwardingUser" ">> <@originalUser> test",
            readMessage message
        )

    [<Fact>]
    let multilineForward(): unit =
        let forwardedMessage = createMessage (Some originalUser) (Some "test\ntest")
        let message = createForwardedMessage forwardingUser forwardedMessage

        Assert.Equal(
            telegramMessage "@forwardingUser" ">> <@originalUser> test\n>> test",
            readMessage message
        )

    [<Fact>]
    let readUnknownSticker(): unit =
        let message = createStickerMessage originalUser None
        Assert.Equal(
            telegramMessage "@originalUser" "[Sticker UNKNOWN]",
            readMessage message
        )

    [<Fact>]
    let readSticker(): unit =
        let message = createStickerMessage originalUser (Some "🐙")
        Assert.Equal(
            telegramMessage "@originalUser" "[Sticker 🐙]",
            readMessage message
        )

    [<Fact>]
    let readCaption(): unit =
        let message = createMessageWithCaption originalUser "test"
        Assert.Equal(
            telegramMessage "@originalUser" "[Content with caption \"test\"]",
            readMessage message
        )

    [<Fact>]
    let readCaptionLinkMessage() =
        let message = { createMessageWithCaption originalUser "Original text" with
                            CaptionEntities = createEntities "text_link" 0L 8L "https://example.com" }

        Assert.Equal(
            telegramMessage "@originalUser" "[Content with caption \"Original [https://example.com] text\"]",
            readMessage message
        )

    [<Fact>]
    let messageFromBotShouldBeUnwrapped(): unit =
        let originalMessage = { defaultMessage with
                                    From = Some { createUser None "" None
                                                      with Id = selfUserId }
                                    Text = Some "Myself\nTests"
                                    Entities = Some <| seq {
                                        yield { Type = "bold"
                                                Offset = 0L
                                                Length = int64 "Myself".Length
                                                Url = None
                                                User = None } } }
        let reply = createReplyMessage (Some replyingUser) (Some "reply text") originalMessage
        Assert.Equal(
            telegramReplyMessage "@replyingUser" "reply text" (telegramMessage "Myself" "Tests").main,
            readMessage reply
        )

module ProcessMessageTests =
    let private processMessage = Funogram.processMessage {| SelfUserId = selfUserId; GroupId = groupId |}

    [<Fact>]
    let messageFromOtherChatShouldBeIgnored(): unit =
        let message = { createMessage (Some originalUser) (Some "test") with
                          Chat = defaultChat }
        Assert.Equal(None, processMessage message)

module FlattenMessageTests =
    let private flattenMessage = Funogram.MessageConverter.flatten Funogram.MessageConverter.DefaultQuoteSettings
    let private flattenMessageLineLimit limit =
        let defaultSettings = Funogram.MessageConverter.DefaultQuoteSettings
        let limits = { defaultSettings.limits with messageLengthLimit = Some limit }
        let quoteSettings = { defaultSettings with limits = limits }
        Funogram.MessageConverter.flatten quoteSettings

    [<Fact>]
    let flattenReplyMessage() =
        let originalMessage = telegramMessage "@originalUser" "Original text"
        let replyMessage = telegramReplyMessage "@replyingUser" "Reply text" originalMessage.main
        Assert.Equal(
            { author = "@replyingUser"; text = ">> <@originalUser> Original text\nReply text" },
            flattenMessage replyMessage
        )

    [<Fact>]
    let flattenMultilineReplyMessage() =
        let originalMessage = telegramMessage "@originalUser" "1\n2\n3"
        let replyMessage = telegramReplyMessage "@replyingUser" "Reply text" originalMessage.main
        Assert.Equal(
            { author = "@replyingUser"; text = ">> <@originalUser> 1\n>> 2\n>> 3\nReply text" },
            flattenMessage replyMessage
        )

    [<Fact>]
    let flattenOverquotedReplyMessage() =
        let originalMessage = telegramMessage "@originalUser" "1\n2\n3\n4"
        let replyMessage = telegramReplyMessage "@replyingUser" "Reply text" originalMessage.main
        Assert.Equal(
            { author = "@replyingUser"; text = ">> <@originalUser> 1\n>> 2\n>> […]\nReply text" },
            flattenMessage replyMessage
        )

    [<Fact>]
    let flattenOverlongReplyMessage() =
        let originalMessage = telegramMessage "@originalUser" "1234567890"
        let replyMessage = telegramReplyMessage "@replyingUser" "Reply text" originalMessage.main

        Assert.Equal(
            { author = "@replyingUser"; text = ">> <@originalUser> 12[…]\nReply text" },
            flattenMessageLineLimit 5 replyMessage
        )

module PrepareMessageTests =
    [<Fact>]
    let prepareMessageEscapesHtml() =
        Assert.Equal(
            "<b>user &lt;3</b>\nmymessage &lt;&amp;&gt;",
            Funogram.prepareHtmlMessage { author = "user <3"; text = "mymessage <&>" }
        )
