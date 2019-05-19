namespace Emulsion.Tests.Telegram.TelegramClient

open Funogram.Types
open Xunit

open Emulsion
open Emulsion.Telegram

module ConvertMessageTests =
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

    [<Fact>]
    let convertMessageWithUnknownUser() =
        Assert.Equal(
            { author = "[UNKNOWN USER]"; text = "" },
            TelegramClient.convertMessage (createMessage None (Some ""))
        )

    [<Fact>]
    let convertMessageWithKnownUsername() =
        let user = createUser (Some "Username") "" None
        Assert.Equal(
            { author = "@Username"; text = "" },
            TelegramClient.convertMessage (createMessage (Some user) (Some ""))
        )

    [<Fact>]
    let convertMessageWithFullName() =
        let user = createUser None "FirstName" (Some "LastName")
        Assert.Equal(
            { author = "FirstName LastName"; text = "" },
            TelegramClient.convertMessage (createMessage (Some user) (Some ""))
        )

    [<Fact>]
    let convertMessageWithFirstNameOnly() =
        let user = createUser None "FirstName" None
        Assert.Equal(
            { author = "FirstName"; text = "" },
            TelegramClient.convertMessage (createMessage (Some user) (Some ""))
        )

    [<Fact>]
    let convertMessageWithText() =
        Assert.Equal(
            { author = "[UNKNOWN USER]"; text = "text" },
            TelegramClient.convertMessage (createMessage None (Some "text"))
        )

    [<Fact>]
    let convertMessageWithoutText() =
        Assert.Equal(
            { author = "[UNKNOWN USER]"; text = "[DATA UNRECOGNIZED]" },
            TelegramClient.convertMessage (createMessage None None)
        )

module PrepareMessageTests =
    [<Fact>]
    let prepareMessageEscapesHtml() =
        Assert.Equal(
            "<b>user &lt;3</b>\nmymessage &lt;&amp;&gt;",
            TelegramClient.prepareHtmlMessage { author = "user <3"; text = "mymessage <&>" }
        )
