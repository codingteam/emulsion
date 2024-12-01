// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

module Emulsion.Tests.Telegram.Client

open System

open Funogram.Telegram.Types
open Funogram.Types
open Serilog.Core
open Xunit

open Emulsion.Messaging
open Emulsion.Telegram
open Emulsion.Telegram.Funogram

[<Literal>]
let private selfUserId = 100500L
[<Literal>]
let private groupId = 200600L
[<Literal>]
let private chatName = "test_room"

let private createUser username firstName lastName = User.Create(
    id = 0L,
    isBot = false,
    firstName = firstName,
    ?lastName = lastName,
    ?username = username
)

let private currentChat = Chat.Create(
    id = groupId,
    ``type`` = ChatType.SuperGroup,
    username = chatName
)

let private defaultMessage = Message.Create(
    messageId = 1L,
    date = DateTime.MinValue,
    chat = currentChat
)

let private createMessage from text : Funogram.Telegram.Types.Message =
    { defaultMessage with
        From = from
        Text = text }

let private createEmptyMessage from : Funogram.Telegram.Types.Message =
    { defaultMessage with
        From = Some from }

let private createReplyMessage from text replyTo : Funogram.Telegram.Types.Message =
    { createMessage from text with
        ReplyToMessage = (Some replyTo) }

let private createForwardedMessage from (forwarded: Funogram.Telegram.Types.Message) =
    { defaultMessage with
        From = Some from
        ForwardOrigin =
            forwarded.From
            |> Option.map (
                fun u -> MessageOrigin.User(
                    MessageOriginUser.Create(``type`` = "user", date = forwarded.Date, senderUser = u)
                )
            )
        Text = forwarded.Text }

let private createStickerMessage from (emoji: string option) =
    { defaultMessage with
        From = Some from
        Chat = currentChat
        Sticker = Some <| Sticker.Create(
            fileId = "",
            fileUniqueId = "",
            ``type`` = "",
            width = 0,
            height = 0,
            isAnimated = false,
            isVideo = false,
            ?emoji = emoji
        )
    }

let private createPhoto() = [|
    {
        FileId = ""
        FileUniqueId = ""
        Width = 0
        Height = 0
        FileSize = None
    }
|]

let private createAnimation() = Animation.Create(
    fileId = "",
    fileUniqueId = "",
    width = 0L,
    height = 0L,
    duration = 0L
)

let private createMessageWithCaption from caption =
    { defaultMessage with
        From = Some from
        Caption = Some caption }

let private createPoll from (question: string) (options: string[]) =
    let options =
        options
        |> Array.map (fun opt -> {
            Text = opt
            VoterCount = 0
            TextEntities = Some [| |]
        })

    let poll= Poll.Create(
        id = "",
        question = question,
        options = options,
        totalVoterCount = 0L,
        isClosed = false,
        isAnonymous = false,
        ``type`` = "",
        allowsMultipleAnswers = false
    )

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

let private createEntity t offset length url = MessageEntity.Create(
    ``type`` = t,
    offset = offset,
    length = length,
    url = url
)

let private createEntities t offset length url = Some <| [|
    createEntity t offset length url
|]

let private originalUser = createUser (Some "originalUser") "" None
let private replyingUser = createUser (Some "replyingUser") "" None
let private forwardingUser = createUser (Some "forwardingUser") "" None

module ReadMessageTests =
    let private readMessage m =
        let links = Async.RunSynchronously(LinkGenerator.gatherLinks Logger.None None None m)
        MessageConverter.read selfUserId (m, links)

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
            readMessage expectedMessage
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
                            Entities = Some <| [| createEntity "text_link" 0L 8L "https://example.com/1"
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
                            Entities = Some <| [| createEntity "text_link" 0L 8L "https://example.com/1"
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
                            Entities = Some <| [| MessageEntity.Create(
                                                    ``type`` = "text_link",
                                                    offset = 0L,
                                                    length = 5L
                                                  ) |] }
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
        let telegramMessageText = $">> <@originalUser> {quotedMultilineMessage}"

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
        let telegramMessageText = $">> <@originalUser> {longString}"

        Assert.Equal(
            authoredTelegramMessage "@forwardingUser" telegramMessageText,
            readMessage message
        )

    [<Fact>]
    let forwardFromHiddenUser(): unit =
        let message = { createEmptyMessage forwardingUser with
                            ForwardOrigin = Some(
                                HiddenUser(
                                    MessageOriginHiddenUser.Create(
                                        ``type`` = "hidden_user",
                                        date = DateTime.MinValue,
                                        senderUserName = "Hidden user"
                                    )
                                )
                            )
                            Text = Some "test" }

        Assert.Equal(
            authoredTelegramMessage "@forwardingUser" ">> <Hidden user> test",
            readMessage message
        )

    [<Fact>]
    let forwardFromChat(): unit =
        let message = { createEmptyMessage forwardingUser with
                            ForwardOrigin = Some(
                                MessageOrigin.Chat(
                                    MessageOriginChat.Create(
                                        ``type`` = "chat",
                                        date = DateTime.MinValue,
                                        senderChat = currentChat
                                    )
                                )
                            )
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
                            NewChatMembers = Some <| [| originalUser |] }
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
                            NewChatMembers = Some <| [| newUser |] }
        Assert.Equal(
            eventTelegramMessage "@originalUser has added FirstName1 the chat",
            readMessage message
        )

    [<Fact>]
    let readNewChatMembers() =
        let newUsers = [|
            createUser None "FirstName1" None
            createUser None "FirstName2" None
        |]
        let message = { createEmptyMessage originalUser with NewChatMembers = Some newUsers }

        Assert.Equal(
            eventTelegramMessage "@originalUser has added FirstName1 and FirstName2 to the chat",
            readMessage message
        )

    [<Fact>]
    let readMoreChatMembers() =
        let newUsers = [|
            createUser None "FirstName1" None
            createUser None "FirstName2" None
            createUser None "FirstName3" None
        |]
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
                                    Entities = Some <| [|
                                        MessageEntity.Create(
                                            ``type`` = "bold",
                                            offset = 0L,
                                            length = int64 "Myself".Length
                                        )
                                    |]
                              }
        let reply = createReplyMessage (Some replyingUser) (Some "reply text") originalMessage
        Assert.Equal(
            authoredTelegramReplyMessage "@replyingUser" "reply text" (authoredTelegramMessage "Myself" "Tests").main,
            readMessage reply
        )

    [<Fact>]
    let ``Reply to forum topic creation shouldn't be taken into account``(): unit =
        let originalMessage = {
            defaultMessage with
                ForumTopicCreated = Some <| ForumTopicCreated.Create("Topic", 0L)
        }
        let replyMessage = createReplyMessage (Some replyingUser) (Some "text") originalMessage
        Assert.Equal(
            authoredTelegramMessage "@replyingUser" "text",
            readMessage replyMessage
        )

    [<Fact>]
    let ``Partial quote is properly preserved``() =
        let originalMessage = {
            defaultMessage with
                From = Some originalUser
                Text = Some "foo bar baz"
        }
        let replyMessage = {
            defaultMessage with
                From = Some replyingUser
                ReplyToMessage = Some originalMessage
                Quote = Some {
                    Entities = None
                    Position = 3
                    Text = "bar"
                    IsManual = None
                }
                Text = Some "Reply text"
        }

        Assert.Equal(
            authoredTelegramReplyMessage "@replyingUser" "Reply text"
                (authoredTelegramMessage "@originalUser" "bar").main,
            readMessage replyMessage
        )

    [<Fact>]
    let ``Partial quote in own message is properly preserved``() =
        let replyMessage = {
            defaultMessage with
                From = Some replyingUser
                ReplyToMessage = Some {
                    defaultMessage with
                        From = Some <| User.Create(selfUserId, isBot = true, firstName = "")
                        Text = Some "xmppUser\nfoo bar"
                        Entities = Some [|
                            createEntity "bold" 0 "xmppUser".Length ""
                        |]
                }
                Quote = Some {
                    Entities = Some [|
                        createEntity "bold" 0 "ppUser".Length ""
                    |]
                    Position = 2
                    Text = "ppUser\nfoo"
                    IsManual = Some true
                }
                Text = Some "Reply text"
        }

        Assert.Equal(
            authoredTelegramReplyMessage "@replyingUser" "Reply text"
                (authoredTelegramMessage "xmppUser" "foo").main,
            readMessage replyMessage
        )

module ProcessMessageTests =
    let private processMessageOpt o =
        processMessage Logger.None None None o

    let private processMessage =
        processMessageOpt {| SelfUserId = selfUserId; GroupId = groupId; MessageThreadId = None |}

    [<Fact>]
    let ``Message with correct group is not ignored``(): unit =
        let message = {
            createMessage (Some originalUser) (Some "test") with
                Chat = Chat.Create(
                  id = groupId,
                  ``type`` = ChatType.SuperGroup
                )
        }
        Assert.True(Option.isSome <| processMessage message)

    [<Fact>]
    let ``Message from other chat is ignored``(): unit =
        let message = { createMessage (Some originalUser) (Some "test") with
                          Chat = Chat.Create(
                              id = 0L,
                              ``type`` = ChatType.SuperGroup
                          ) }
        Assert.Equal(None, processMessage message)

    [<Fact>]
    let ``Message from incorrect thread is ignored``(): unit =
        let message = {
            createMessage (Some originalUser) (Some "test") with
                Chat = Chat.Create(
                  id = groupId,
                  ``type`` = ChatType.SuperGroup
                )
                MessageThreadId = Some 123L

        }
        Assert.Equal(None, processMessageOpt {|
            SelfUserId = selfUserId
            GroupId = groupId
            MessageThreadId = Some 234L
        |} message)

    let ``Message with any thread id is not ignored if thread id is not set``(): unit =
        let message = {
            createMessage (Some originalUser) (Some "test") with
                Chat = Chat.Create(
                  id = groupId,
                  ``type`` = ChatType.SuperGroup
                )
                MessageThreadId = Some 123L
        }
        Assert.True(Option.isSome <| processMessage message)

    let ``Message with correct thread id is not ignored``(): unit =
        let threadId = 236L
        let message = {
            createMessage (Some originalUser) (Some "test") with
                Chat = Chat.Create(
                  id = groupId,
                  ``type`` = ChatType.SuperGroup
                )
                MessageThreadId = Some threadId
        }
        Assert.True(Option.isSome <| processMessageOpt {|
            SelfUserId = selfUserId
            GroupId = groupId
            MessageThreadId = Some threadId
        |} message)

module ProcessSendResultTests =
    [<Fact>]
    let processResultShouldDoNothingOnOk(): unit =
        Assert.Equal((), processSendResult(Ok()))

    [<Fact>]
    let processResultShouldThrowOnError(): unit =
        let result = Error({ ErrorCode = 502; Description = "Error" })
        Assert.ThrowsAny<Exception>(Action(fun () -> processSendResult result)) |> ignore

module FlattenMessageTests =
    let private flattenMessage = MessageConverter.flatten MessageConverter.DefaultQuoteSettings
    let private flattenMessageLineLimit limit =
        let defaultSettings = MessageConverter.DefaultQuoteSettings
        let limits = { defaultSettings.limits with messageLengthLimit = Some limit }
        let quoteSettings = { defaultSettings with limits = limits }
        MessageConverter.flatten quoteSettings

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
            "<b>user &lt;3</b>\nmy_message &lt;&amp;&gt;",
            prepareHtmlMessage (Authored { author = "user <3"; text = "my_message <&>" })
        )
