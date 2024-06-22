// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

namespace Emulsion.Telegram

open System
open System.Threading

open Emulsion.Database
open Emulsion.Messaging.MessageSystem
open Emulsion.Settings

type FileInfo = {
    TemporaryLink: Uri
    Size: uint64
}

type ITelegramClient =
    abstract GetFileInfo: fileId: string -> Async<FileInfo option>

type Client(ctx: ServiceContext,
            cancellationToken: CancellationToken,
            telegramSettings: TelegramSettings,
            databaseSettings: DatabaseSettings option,
            hostingSettings: HostingSettings option) =
    inherit MessageSystemBase(ctx, cancellationToken)

    let botConfig = {
        Funogram.Telegram.Bot.Config.defaultConfig with
            Token = telegramSettings.Token
            OnError = fun e -> ctx.Logger.Error(e, "Exception in Telegram message processing")
    }

    interface ITelegramClient with
        member this.GetFileInfo(fileId) = async {
            let logger = ctx.Logger
            logger.Information("Querying file information for file {FileId}", fileId)
            let! file = Funogram.sendGetFile botConfig fileId
            match file.FilePath, file.FileSize with
            | None, None ->
                logger.Warning("File {FileId} was not found on server", fileId)
                return None
            | Some fp, Some sz ->
                return Some {
                    TemporaryLink = Uri $"https://api.telegram.org/file/bot{telegramSettings.Token}/{fp}"
                    Size = Checked.uint64 sz
                }
            | x, y -> return failwith $"Unknown data received from Telegram server: {x}, {y}"
        }

    override _.RunUntilError receiver = async {
        // Run loop of Telegram is in no need of any complicated start, so just return an async that will perform it:
        return Funogram.run ctx.Logger telegramSettings databaseSettings hostingSettings botConfig receiver
    }

    override _.Send message =
        Funogram.sendMessage telegramSettings botConfig message
