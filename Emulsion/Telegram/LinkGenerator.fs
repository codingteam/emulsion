/// A module that generates links to various content from Telegram.
module Emulsion.Telegram.LinkGenerator

open System

open Funogram.Telegram.Types
open Serilog

open Emulsion.ContentProxy
open Emulsion.Database
open Emulsion.Settings

type FunogramMessage = Funogram.Telegram.Types.Message

type TelegramThreadLinks = {
    ContentLinks: Uri seq
    ReplyToContentLinks: Uri seq
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

let private getFileIds(message: FunogramMessage): string seq =
    let allFileIds = ResizeArray()
    let inline extractFileId(o: ^a option) =
        Option.iter(fun o -> allFileIds.Add((^a) : (member FileId: string) o)) o

    let extractPhotoFileIds: PhotoSize seq option -> unit =
        Option.iter(
            Seq.map(fun photoSize -> photoSize.FileId)
            >> Seq.distinct
            >> Seq.iter(allFileIds.Add)
        )

    extractFileId message.Document
    extractFileId message.Audio
    extractFileId message.Animation
    extractPhotoFileIds message.Photo
    extractFileId message.Sticker
    extractFileId message.Video
    extractFileId message.Voice
    extractFileId message.VideoNote

    allFileIds

let private getContentIdentities message: ContentStorage.MessageContentIdentity seq =
    match message.Chat with
    | { Type = SuperGroup
        Username = Some chatName } ->
        getFileIds message
        |> Seq.map (fun fileId ->
            {
                ChatUserName = chatName
                MessageId = message.MessageId
                FileId = fileId
            }
       )
    | _ -> Seq.empty

let gatherLinks (logger: ILogger)
                (databaseSettings: DatabaseSettings option)
                (hostingSettings: HostingSettings option)
                (message: FunogramMessage): Async<TelegramThreadLinks> = async {
    let getMessageBodyLinks message: Async<Uri seq> =
        match databaseSettings, hostingSettings with
        | Some databaseSettings, Some hostingSettings ->
            async {
                let! links =
                    getContentIdentities message
                    |> Seq.map(fun identity -> async {
                        let! content = DataStorage.transaction databaseSettings (fun ctx ->
                            ContentStorage.getOrCreateMessageRecord ctx identity
                        )

                        let hashId = Proxy.encodeHashId hostingSettings.HashIdSalt content.Id
                        return Proxy.getLink hostingSettings.ExternalUriBase hashId
                    })
                    |> Async.Parallel
                return links
            }
        | _ ->
            let link = gatherMessageLink message
            async.Return(Option.toList link)

    try
        let! contentLink = getMessageBodyLinks message
        let! replyToContentLink =
            match message.ReplyToMessage with
            | None -> async.Return Seq.empty
            | Some replyTo -> getMessageBodyLinks replyTo
        return {
            ContentLinks = contentLink
            ReplyToContentLinks = replyToContentLink
        }
    with
    | ex ->
        logger.Error(ex, "Error while trying to generate links for message.")
        return {
            ContentLinks = Seq.empty
            ReplyToContentLinks = Seq.empty
        }
}
