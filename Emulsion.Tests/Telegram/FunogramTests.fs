module Emulsion.Tests.Telegram.Client

open System

open Funogram.Telegram.Types
open Xunit

open Emulsion
open Emulsion.Telegram
open Emulsion.Telegram.Funogram

[<Literal>]
let private selfUserId = 100500L
[<Literal>]
let private groupId = 200600L
[<Literal>]
let private chatName = "test_room"

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
        Username = Some chatName
        Type = SuperGroup
}

let private createMessage from text : Funogram.Telegram.Types.Message =
    { defaultMessage with
        From = from
        Chat = currentChat
        Text = text }

let private createEmptyMessage from : Funogram.Telegram.Types.Message =
    { defaultMessage with
        From = Some from
        Chat = currentChat }

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

let private createPhoto() = seq {
    { FileId = ""
      Width = 0
      Height = 0
      FileSize = None
    }
}

let private createAnimation() =
    { FileId = ""
      Width = 0
      Height = 0
      Duration = 0
      Thumb = None
      FileName = None
      MimeType = None
      FileSize = None }

let private createMessageWithCaption from caption =
    { defaultMessage with
        From = Some from
        Chat = currentChat
        Caption = Some caption }

let private createPoll from (question: string) (options: string[]) =
    let options =
        options
        |> Array.map (fun opt -> {
            Text = opt
            VoterCount = 0
        })

    let poll: Poll =
        { Id = ""
          Question = question
          Options = options
          IsClosed = false }

    { defaultMessage with
        From = Some from
        Chat = currentChat
        Poll = Some poll }

let private authoredTelegramMessage author text =
    { main = Authored { author = author; text = text }; replyTo = None }

let private authoredTelegramReplyMessage author text replyTo =
    { main = Authored { author = author; text = text }; replyTo = Some replyTo }

let private eventTelegramMessage text =
    { main = Event { text = text }; replyTo = None }

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
            authoredTelegramMessage "[UNKNOWN USER]" "",
            readMessage (createMessage None (Some ""))
        )

    [<Fact>]
    let readMessageWithKnownUsername() =
        let user = createUser (Some "Username") "" None
        Assert.Equal(
            authoredTelegramMessage "@Username" "",
            readMessage (createMessage (Some user) (Some ""))
        )

    [<Fact>]
    let readMessageWithFullName() =
        let user = createUser None "FirstName" (Some "LastName")
        Assert.Equal(
            authoredTelegramMessage "FirstName LastName" "",
            readMessage (createMessage (Some user) (Some ""))
        )

    [<Fact>]
    let readMessageWithFirstNameOnly() =
        let user = createUser None "FirstName" None
        Assert.Equal(
            authoredTelegramMessage "FirstName" "",
            readMessage (createMessage (Some user) (Some ""))
        )

    [<Fact>]
    let readMessageWithText() =
        Assert.Equal(
            authoredTelegramMessage "[UNKNOWN USER]" "text",
            readMessage (createMessage None (Some "text"))
        )

    [<Fact>]
    let readMessageWithoutText() =
        Assert.Equal(
            authoredTelegramMessage "[UNKNOWN USER]" "[DATA UNRECOGNIZED]: https://t.me/test_room/1",
            readMessage (createMessage None None)
        )

    [<Fact>]
    let readMessageWithoutTextAndLink() =
        let privateChat = { currentChat with Type = ChatType.Private }
        let expectedMessage = { createMessage None None with Chat = privateChat }
        Assert.Equal(
            authoredTelegramMessage "[UNKNOWN USER]" "[DATA UNRECOGNIZED]",
            readMessage (expectedMessage)
        )

    [<Fact>]
    let readReplyMessage() =
        let originalMessage = createMessage (Some originalUser) (Some "Original text")
        let replyMessage = createReplyMessage (Some replyingUser) (Some "Reply text") originalMessage

        Assert.Equal(
            { main = Authored { author = "@replyingUser"; text = "Reply text" }
              replyTo = Some (Authored { author = "@originalUser"; text = "Original text" }) },
            readMessage replyMessage
        )

    [<Fact>]
    let readTextLinkMessage() =
        let message = { createMessage (Some originalUser) (Some "Original text") with
                            Entities = createEntities "text_link" 0L 8L "https://example.com" }

        Assert.Equal(
            authoredTelegramMessage "@originalUser" "Original [https://example.com] text",
            readMessage message
        )

    [<Fact>]
    let readMultipleTextLinksMessage() =
        let message = { createMessage (Some originalUser) (Some "Original text") with
                            Entities = Some <| upcast [| createEntity "text_link" 0L 8L "https://example.com/1"
                                                         createEntity "text_link" 9L 4L "https://example.com/2" |] }
        Assert.Equal(
            authoredTelegramMessage "@originalUser" "Original [https://example.com/1] text [https://example.com/2]",
            readMessage message
        )

    [<Fact>]
    let readInvalidTextLinkMessage() =
        let message = { createMessage (Some originalUser) (Some "Original text") with
                            Entities = createEntities "text_link" 0L 800L "https://example.com" }
        Assert.Equal(
            authoredTelegramMessage "@originalUser" "Original text [https://example.com]",
            readMessage message
        )

    [<Fact>]
    let readNegativeOffsetTextLinkMessage() =
        let message = { createMessage (Some originalUser) (Some "Original text") with
                            Entities = createEntities "text_link" -1L 5L "https://example.com" }
        Assert.Equal(
            authoredTelegramMessage "@originalUser" "Original text",
            readMessage message
        )

    [<Fact>]
    let readZeroLengthTextLinkMessage() =
        let message = { createMessage (Some originalUser) (Some "Original text") with
                            Entities = createEntities "text_link" 0L 0L "https://example.com" }
        Assert.Equal(
            authoredTelegramMessage "@originalUser" "Original text",
            readMessage message
        )

    [<Fact>]
    let readSuperLongLengthTextLinkMessage() =
        let message = { createMessage (Some originalUser) (Some "Original text") with
                            Entities = createEntities "text_link" Int64.MaxValue Int64.MaxValue "https://example.com" }
        Assert.Equal(
            authoredTelegramMessage "@originalUser" "Original text",
            readMessage message
        )

    [<Fact>]
    let readOverlappingTextLinksMessage() =
        let message = { createMessage (Some originalUser) (Some "Original text") with
                            Entities = Some <| upcast [| createEntity "text_link" 0L 8L "https://example.com/1"
                                                         createEntity "text_link" 0L 13L "https://example.com/2" |] }
        Assert.Equal(
            authoredTelegramMessage "@originalUser" "Original [https://example.com/1] text [https://example.com/2]",
            readMessage message
        )

    [<Fact>]
    let readNonTextLinkMessage() =
        let message = { createMessage (Some originalUser) (Some "Original text") with
                            Entities = createEntities "not_link" 0L 0L "https://example.com" }
        Assert.Equal(
            authoredTelegramMessage "@originalUser" "Original text",
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
            authoredTelegramMessage "@originalUser" "Original text",
            readMessage message
        )

    [<Fact>]
    let forward(): unit =
        let forwardedMessage = createMessage (Some originalUser) (Some "test")
        let message = createForwardedMessage forwardingUser forwardedMessage

        Assert.Equal(
            authoredTelegramMessage "@forwardingUser" ">> <@originalUser> test",
            readMessage message
        )

    [<Fact>]
    let multilineForward(): unit =
        let forwardedMessage = createMessage (Some originalUser) (Some "test\ntest")
        let message = createForwardedMessage forwardingUser forwardedMessage

        Assert.Equal(
            authoredTelegramMessage "@forwardingUser" ">> <@originalUser> test\n>> test",
            readMessage message
        )

    [<Fact>]
    let multilineForwardShouldBeUnlimited(): unit =
        let messageLinesLimit = MessageConverter.DefaultMessageLinesLimit
        let multilineMessage = String.init messageLinesLimit (fun _ -> "test\n") + "test"
        let forwardedMessage = createMessage (Some originalUser) (Some multilineMessage)
        let message = createForwardedMessage forwardingUser forwardedMessage
        let quotedMultilineMessage = "test" + String.init messageLinesLimit (fun _ -> "\n>> test")
        let telegramMessageText = sprintf ">> <@originalUser> %s" quotedMultilineMessage

        Assert.Equal(
            authoredTelegramMessage "@forwardingUser" telegramMessageText,
            readMessage message
        )

    [<Fact>]
    let longForwardShouldBeUnlimited(): unit =
        let messageLengthLimit = MessageConverter.DefaultMessageLengthLimit
        let longString = String.init (messageLengthLimit + 1) (fun _ -> "A")
        let forwardedMessage = createMessage (Some originalUser) (Some longString)
        let message = createForwardedMessage forwardingUser forwardedMessage
        let telegramMessageText = sprintf ">> <@originalUser> %s" longString

        Assert.Equal(
            authoredTelegramMessage "@forwardingUser" telegramMessageText,
            readMessage message
        )

    [<Fact>]
    let forwardFromHiddenUser(): unit =
        let message = { createEmptyMessage forwardingUser with
                            ForwardSenderName = Some "Hidden user"
                            Text = Some "test" }

        Assert.Equal(
            authoredTelegramMessage "@forwardingUser" ">> <Hidden user> test",
            readMessage message
        )

    [<Fact>]
    let forwardFromChat(): unit =
        let message = { createEmptyMessage forwardingUser with
                            ForwardFromChat = Some currentChat
                            Text = Some "test" }

        Assert.Equal(
            authoredTelegramMessage "@forwardingUser" ">> <@test_room> test",
            readMessage message
        )


    [<Fact>]
    let readUnknownSticker(): unit =
        let message = createStickerMessage originalUser None
        Assert.Equal(
            authoredTelegramMessage "@originalUser" "[Sticker UNKNOWN]: https://t.me/test_room/1",
            readMessage message
        )

    [<Fact>]
    let readSticker(): unit =
        let message = createStickerMessage originalUser (Some "🐙")
        Assert.Equal(
            authoredTelegramMessage "@originalUser" "[Sticker 🐙]: https://t.me/test_room/1",
            readMessage message
        )

    [<Fact>]
    let readCaption(): unit =
        let message = createMessageWithCaption originalUser "test"
        Assert.Equal(
            authoredTelegramMessage "@originalUser" "[Unknown content with caption \"test\"]: https://t.me/test_room/1",
            readMessage message
        )

    [<Fact>]
    let readCaptionLinkMessage() =
        let message = { createMessageWithCaption originalUser "Original text" with
                            CaptionEntities = createEntities "text_link" 0L 8L "https://example.com" }

        Assert.Equal(
            authoredTelegramMessage "@originalUser" "[Unknown content with caption \"Original [https://example.com] text\"]: https://t.me/test_room/1",
            readMessage message
        )

    [<Fact>]
    let readPhotoMessage() =
        let message = { createEmptyMessage originalUser with
                            Photo = Some <| createPhoto() }
        Assert.Equal(
            authoredTelegramMessage "@originalUser" "[Photo]: https://t.me/test_room/1",
            readMessage message
        )

    [<Fact>]
    let readPhotoWithCaptionMessage() =
        let message = { createMessageWithCaption originalUser "test" with
                            Photo = Some <| createPhoto() }
        Assert.Equal(
            authoredTelegramMessage "@originalUser" "[Photo with caption \"test\"]: https://t.me/test_room/1",
            readMessage message
        )

    [<Fact>]
    let readAnimationMessage() =
        let message = { createEmptyMessage originalUser with
                            Animation = Some <| createAnimation() }
        Assert.Equal(
            authoredTelegramMessage "@originalUser" "[Animation]: https://t.me/test_room/1",
            readMessage message
        )

    [<Fact>]
    let readAnimationWithCaptionMessage() =
        let message = { createMessageWithCaption originalUser "test" with
                            Animation = Some <| createAnimation() }
        Assert.Equal(
            authoredTelegramMessage "@originalUser" "[Animation with caption \"test\"]: https://t.me/test_room/1",
            readMessage message
        )

    [<Fact>]
    let readContentWithoutLink() =
        let privateChat = { currentChat with Type = ChatType.Private }
        let message = { createMessageWithCaption originalUser "test" with Chat = privateChat }
        Assert.Equal(
            authoredTelegramMessage "@originalUser" "[Unknown content with caption \"test\"]",
            readMessage message
        )

    [<Fact>]
    let readPollMessage() =
        let message = createPoll originalUser "Question?" [|"Option 1"; "Option 2"|]
        Assert.Equal(
            authoredTelegramMessage "@originalUser" "[Poll] Question?\n- Option 1\n- Option 2",
            readMessage message
        )

    [<Fact>]
    let readUserEntersChat() =
        let message = { createEmptyMessage originalUser with
                            NewChatMembers = Some <| seq { originalUser } }
        Assert.Equal(
            eventTelegramMessage "@originalUser has entered the chat",
            readMessage message
        )

    [<Fact>]
    let readUserLeftChat() =
        let message = { createEmptyMessage originalUser with
                            LeftChatMember = Some originalUser }
        Assert.Equal(
            eventTelegramMessage "@originalUser has left the chat",
            readMessage message
        )

    let readAddedChatMember() =
        let newUser = createUser None "FirstName1" None
        let message = { createEmptyMessage originalUser with
                            NewChatMembers = Some <| seq { newUser } }
        Assert.Equal(
            eventTelegramMessage "@originalUser has added FirstName1 the chat",
            readMessage message
        )

    [<Fact>]
    let readNewChatMembers() =
        let newUsers = seq {
            createUser None "FirstName1" None
            createUser None "FirstName2" None
        }
        let message = { createEmptyMessage originalUser with NewChatMembers = Some newUsers }

        Assert.Equal(
            eventTelegramMessage "@originalUser has added FirstName1 and FirstName2 to the chat",
            readMessage message
        )

    [<Fact>]
    let readMoreChatMembers() =
        let newUsers = seq {
            createUser None "FirstName1" None
            createUser None "FirstName2" None
            createUser None "FirstName3" None
        }
        let message = { createEmptyMessage originalUser with NewChatMembers = Some newUsers }

        Assert.Equal(
            eventTelegramMessage "@originalUser has added FirstName1, FirstName2, and FirstName3 to the chat",
            readMessage message
        )

    [<Fact>]
    let readLeftChatMember() =
        let user = createUser None "FirstName1" None
        let message = { createEmptyMessage originalUser with LeftChatMember = Some user }

        Assert.Equal(
            eventTelegramMessage "@originalUser has removed FirstName1 from the chat",
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
            authoredTelegramReplyMessage "@replyingUser" "reply text" (authoredTelegramMessage "Myself" "Tests").main,
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
        let originalMessage = authoredTelegramMessage "@originalUser" "Original text"
        let replyMessage = authoredTelegramReplyMessage "@replyingUser" "Reply text" originalMessage.main
        Assert.Equal(
            Authored { author = "@replyingUser"; text = ">> <@originalUser> Original text\n\nReply text" },
            flattenMessage replyMessage
        )

    [<Fact>]
    let flattenReplyEventMessage() =
        let originalMessage = eventTelegramMessage "@originalUser has entered the chat"
        let replyMessage = authoredTelegramReplyMessage "@replyingUser" "Reply text" originalMessage.main
        Assert.Equal(
            Authored { author = "@replyingUser"; text = ">> @originalUser has entered the chat\n\nReply text" },
            flattenMessage replyMessage
        )

    [<Fact>]
    let flattenMultilineReplyMessage() =
        let originalMessage = authoredTelegramMessage "@originalUser" "1\n2\n3"
        let replyMessage = authoredTelegramReplyMessage "@replyingUser" "Reply text" originalMessage.main
        Assert.Equal(
            Authored { author = "@replyingUser"; text = ">> <@originalUser> 1\n>> 2\n>> 3\n\nReply text" },
            flattenMessage replyMessage
        )

    [<Fact>]
    let flattenOverquotedReplyMessage() =
        let originalMessage = authoredTelegramMessage "@originalUser" "1\n2\n3\n4"
        let replyMessage = authoredTelegramReplyMessage "@replyingUser" "Reply text" originalMessage.main
        Assert.Equal(
            Authored { author = "@replyingUser"; text = ">> <@originalUser> 1\n>> 2\n>> […]\n\nReply text" },
            flattenMessage replyMessage
        )

    [<Fact>]
    let flattenOverlongReplyMessage() =
        let originalMessage = authoredTelegramMessage "@originalUser" "1234567890"
        let replyMessage = authoredTelegramReplyMessage "@replyingUser" "Reply text" originalMessage.main

        Assert.Equal(
            Authored { author = "@replyingUser"; text = ">> <@originalUser> 12[…]\n\nReply text" },
            flattenMessageLineLimit 5 replyMessage
        )

module PrepareMessageTests =
    [<Fact>]
    let prepareMessageEscapesHtml() =
        Assert.Equal(
            "<b>user &lt;3</b>\nmymessage &lt;&amp;&gt;",
            Funogram.prepareHtmlMessage (Authored { author = "user <3"; text = "mymessage <&>" })
        )
