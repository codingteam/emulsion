/// A module that generates links to various content from Telegram.
module Emulsion.Telegram.LinkGenerator

open Emulsion.ContentProxy
open Emulsion.Database
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

let private getFileId(message: FunogramMessage) =
    match message with
    | { Animation = Some { FileId = fileId } } -> Some fileId
    | _ -> None

let private getMessageIdentity message: ContentStorage.MessageIdentity option =
    let fileId = getFileId message
    match fileId, message.Chat with
    | Some fileId, { Type = SuperGroup
                     Username = Some chatName } ->
        Some {
            ChatUserName = chatName
            MessageId = message.MessageId
            FileId = fileId
        }
    | _, _ -> None

// TODO: right type for the hostingSettings
let gatherLinks (databaseSettings: DatabaseSettings option) (message: FunogramMessage): Async<TelegramThreadLinks> = async {
    let getMessageBodyLink message =
        match databaseSettings with
        | None ->
            let link = gatherMessageLink message
            async.Return link
        | Some settings ->
            let identity = getMessageIdentity message
            match identity with
            | None -> async.Return None
            | Some id ->
                async {
                    let! content = DataStorage.transaction settings (fun ctx ->
                        ContentStorage.getOrCreateMessageRecord ctx id
                    )
                    return Some(failwithf "TODO: Generate URL from hosting settings")
                }

    let! contentLink = getMessageBodyLink message
    let! replyToContentLink =
        match message.ReplyToMessage with
        | None -> async.Return None
        | Some m -> getMessageBodyLink m

    return {
        ContentLink = contentLink
        ReplyToContentLink = replyToContentLink
    }
}
