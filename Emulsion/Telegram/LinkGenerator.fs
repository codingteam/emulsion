/// A module that generates links to various content from Telegram.
module Emulsion.Telegram.LinkGenerator

open Funogram.Telegram.Types

type FunogramMessage = Funogram.Telegram.Types.Message

type TelegramThreadLinks = {
    ContentLink: string option
    ReplyToContentLink: string option
}

let private getMessageLink (message: FunogramMessage) =
    match message with
    | { MessageId = id
        Chat = { Type = SuperGroup
                 Username = Some chatName } } ->
        Some $"https://t.me/{chatName}/{id}"
    | _ -> None

let private gatherMessageLink(message: FunogramMessage) =
    match message with
    | { Text = Some _} | { Poll = Some _ } -> None
    | _ -> getMessageLink message

let gatherLinks(message: FunogramMessage): Async<TelegramThreadLinks> = async {
    return {
        ContentLink = gatherMessageLink message
        ReplyToContentLink = message.ReplyToMessage |> Option.bind gatherMessageLink
    }
}
