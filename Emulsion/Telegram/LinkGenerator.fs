/// A module that generates links to various content from Telegram.
module Emulsion.Telegram.LinkGenerator

open System

open Funogram.Telegram.Types

open Emulsion.ContentProxy
open Emulsion.Database
open Emulsion.Settings

type FunogramMessage = Funogram.Telegram.Types.Message

type TelegramThreadLinks = {
    ContentLink: Uri option
    ReplyToContentLink: Uri option
}

let private getMessageLink (message: FunogramMessage) =
    match message with
    | { MessageId = id
        Chat = { Type = SuperGroup
                 Username = Some chatName } } ->
        Some <| Uri $"https://t.me/{chatName}/{id}"
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

let gatherLinks (databaseSettings: DatabaseSettings option)
                (hostingSettings: HostingSettings option)
                (message: FunogramMessage): Async<TelegramThreadLinks> = async {
    let getMessageBodyLink message =
        match databaseSettings, hostingSettings with
        | Some databaseSettings, Some hostingSettings ->
            let identity = getMessageIdentity message
            match identity with
            | None -> async.Return None
            | Some id ->
                async {
                    let! content = DataStorage.transaction databaseSettings (fun ctx ->
                        ContentStorage.getOrCreateMessageRecord ctx id
                    )

                    let hashId = Proxy.encodeHashId hostingSettings.HashIdSalt content.Id
                    return Some <| Proxy.getLink hostingSettings.BaseUri hashId
                }
        | _ ->
            let link = gatherMessageLink message
            async.Return link

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
