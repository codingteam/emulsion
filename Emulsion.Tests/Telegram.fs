module Emulsion.Tests.Telegram

open Xunit
open Emulsion
open Funogram.Types

let private createUser username firstName lastName = {
    Id = 0L
    FirstName = firstName
    LastName = lastName
    Username = username
    LanguageCode = None
}

let private createMessage from text : Message =
    { defaultMessage with
        From = from
        Text = text }

[<Fact>]
let convertMessageWithUnknownUser() =
    Assert.Equal({ author = "[UNKNOWN USER]"; text = "" }, Telegram.convertMessage (createMessage None (Some "")))

[<Fact>]
let convertMessageWithKnownUsername() =
    let user = createUser (Some "Username") "" None
    Assert.Equal({ author = "@Username"; text = "" }, Telegram.convertMessage (createMessage (Some user) (Some "")))

[<Fact>]
let convertMessageWithFullName() =
    let user = createUser None "FirstName" (Some "LastName")
    Assert.Equal(
        { author = "FirstName LastName"; text = "" },
        Telegram.convertMessage (createMessage (Some user) (Some ""))
    )

[<Fact>]
let convertMessageWithFirstNameOnly() =
    let user = createUser None "FirstName" None
    Assert.Equal({ author = "FirstName"; text = "" }, Telegram.convertMessage (createMessage (Some user) (Some "")))

[<Fact>]
let convertMessageWithText() =
    Assert.Equal(
        { author = "[UNKNOWN USER]"; text = "text" },
        Telegram.convertMessage (createMessage None (Some "text"))
    )

[<Fact>]
let convertMessageWithoutText() =
    Assert.Equal(
        { author = "[UNKNOWN USER]"; text = "[DATA UNRECOGNIZED]" },
        Telegram.convertMessage (createMessage None None)
    )
