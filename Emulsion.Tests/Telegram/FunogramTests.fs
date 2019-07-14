module Emulsion.Tests.Telegram.Client

open Funogram.Types
open Xunit

open Emulsion
open Emulsion.Telegram

let private createUser username firstName lastName = {
    Id = 0L
    FirstName = firstName
    LastName = lastName
    Username = username
    LanguageCode = None
}

let private createMessage from text : Funogram.Types.Message =
    { defaultMessage with
        From = from
        Text = text }

let private createReplyMessage from text replyTo : Funogram.Types.Message =
    { createMessage from text with
        ReplyToMessage = (Some replyTo) }

let private telegramMessage author text =
    { main = { author = author; text = text }; replyTo = None }

let private telegramReplyMessage author text replyTo =
    { main = { author = author; text = text }; replyTo = Some replyTo }

let private originalUser = createUser (Some "originalUser") "" None
let private replyingUser = createUser (Some "replyingUser") "" None

module ReadMessageTests =
    let readMessage = Funogram.MessageConverter.read

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
