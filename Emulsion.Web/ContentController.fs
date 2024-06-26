// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

namespace Emulsion.Web

open System
open System.Threading.Tasks

open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging

open Emulsion.ContentProxy
open Emulsion.Database
open Emulsion.Database.Entities
open Emulsion.Settings
open Emulsion.Telegram

[<ApiController>]
[<Route("content")>]
type ContentController(logger: ILogger<ContentController>,
                       configuration: HostingSettings,
                       telegram: ITelegramClient,
                       fileCache: Func<FileCache option>,
                       context: EmulsionDbContext) =
    inherit ControllerBase()

    let decodeHashId hashId =
        try
            Some <| Proxy.decodeHashId configuration.HashIdSalt hashId
        with
            | ex ->
                logger.LogWarning(ex, "Error during hashId deserializing")
                None

    [<HttpGet("{hashId}")>]
    member this.Get(hashId: string): Task<IActionResult> = task {
        match decodeHashId hashId with
        | None ->
            logger.LogWarning $"Cannot decode hash id: \"{hashId}\"."
            return this.BadRequest()
        | Some contentId ->
            match! ContentStorage.getById context contentId with
            | None ->
                logger.LogWarning $"Content \"{contentId}\" not found in content storage."
                return this.NotFound() :> IActionResult
            | Some content ->
                match fileCache.Invoke() with
                | None ->
                    match content.ChatUserName with
                    | "" -> return UnprocessableEntityResult()
                    | _ ->
                        let link = $"https://t.me/{content.ChatUserName}/{string content.MessageId}"
                        return RedirectResult link
                | Some cache ->
                    match! telegram.GetFileInfo content.FileId with
                    | None ->
                        logger.LogWarning $"File \"{content.FileId}\" could not be found on Telegram server."
                        return this.NotFound() :> IActionResult
                    | Some fileInfo ->
                        match! cache.Download(fileInfo.TemporaryLink, content.FileId, fileInfo.Size) with
                        | None ->
                            logger.LogWarning $"Link \"{fileInfo}\" could not be downloaded."
                            return this.NotFound() :> IActionResult
                        | Some stream ->
                            match content.MimeType with
                            | "application/octet-stream" -> return FileStreamResult(stream, content.MimeType, FileDownloadName = content.FileName)
                            | _ -> return FileStreamResult(stream, content.MimeType)
    }
