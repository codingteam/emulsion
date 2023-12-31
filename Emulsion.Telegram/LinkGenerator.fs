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

type private FileInfo = {
    FileId: string
    FileName: string option
    MimeType: string option
}

type private WithId<'T when 'T: (member FileId: string)> = 'T
type private WithFileName<'T when 'T: (member FileName: string option)> = 'T
type private WithMimeType<'T when 'T: (member MimeType: string option)> = 'T

let inline private extractFileInfo<'T
                        when WithId<'T>
                        and WithFileName<'T>
                        and WithMimeType<'T>>
                        : 'T option -> FileInfo option =
    Option.map(fun file -> {
        FileId = file.FileId
        FileName = file.FileName
        MimeType = file.MimeType
    })

let inline private extractFileInfoWithName<'T
                                when WithId<'T>
                                and WithMimeType<'T>>
                                (fileName: string)
                                : 'T option -> FileInfo option =
    Option.map(fun file -> {
        FileId = file.FileId
        FileName = Some fileName
        MimeType = file.MimeType
    })

let inline private extractFileInfoWithNameAndMimeType<'T when WithId<'T>>
                                        (fileName: string)
                                        (mimeType: string)
                                        : 'T option -> FileInfo option =
    Option.map(fun file -> {
        FileId = file.FileId
        FileName = Some fileName
        MimeType = Some mimeType
    })

let private extractPhotoFileInfo: PhotoSize[] option -> FileInfo option =
    Option.bind(
        // Telegram may send several differently-sized thumbnails in one message. Pick the biggest one of them.
        Seq.sortByDescending(fun size -> size.Height * size.Width)
        >> Seq.map(fun photoSize -> photoSize.FileId)
        >> Seq.tryHead
        >> Option.map(fun fileId -> {
            FileId = fileId
            FileName = Some "photo.jpg"
            MimeType = Some "image/jpeg"
        })
    )

let private extractStickerFileInfo: Sticker option -> FileInfo option =
    Option.bind(fun sticker ->
        if sticker.IsAnimated then
            // We cannot to preview Telegram's .tgs stickers in browser, so return thumbnail
            extractFileInfoWithNameAndMimeType "sticker.webp" "image/webp" sticker.Thumbnail
        elif sticker.IsVideo then
            extractFileInfoWithNameAndMimeType "sticker.webm" "video/webm" (Some sticker)
        else
            extractFileInfoWithNameAndMimeType "sticker.webp" "image/webp" (Some sticker)
    )

let private getFileInfos(message: FunogramMessage): FileInfo seq =
    Seq.choose id <| seq {
        extractFileInfo message.Document
        extractFileInfo message.Audio
        extractFileInfo message.Animation
        extractPhotoFileInfo message.Photo
        extractStickerFileInfo message.Sticker
        extractFileInfo message.Video
        extractFileInfoWithName "voice.ogg" message.Voice
        extractFileInfoWithNameAndMimeType "video.mp4" "video/mp4" message.VideoNote
    }


let private getContentIdentities(message: FunogramMessage): ContentStorage.MessageContentIdentity seq =
    getFileInfos message
    |> Seq.map (fun fileInfo ->
        {
            ChatId = message.Chat.Id
            ChatUserName = Option.defaultValue "" message.Chat.Username
            MessageId = message.MessageId
            FileId = fileInfo.FileId
            FileName = Option.defaultValue "file.bin" fileInfo.FileName
            MimeType = Option.defaultValue "application/octet-stream" fileInfo.MimeType
        }
   )

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
