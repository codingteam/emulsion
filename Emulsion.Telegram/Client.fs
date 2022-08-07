namespace Emulsion.Telegram

open System
open System.IO
open System.Threading

open Emulsion.ContentProxy
open Emulsion.Database
open Emulsion.Messaging.MessageSystem
open Emulsion.Settings

type ITelegramClient =
    abstract GetTemporaryFileLink: fileId: string -> Async<Stream option>

type Client(ctx: ServiceContext,
            cancellationToken: CancellationToken,
            telegramSettings: TelegramSettings,
            databaseSettings: DatabaseSettings option,
            hostingSettings: HostingSettings option,
            fileCache: FileCache option) =
    inherit MessageSystemBase(ctx, cancellationToken)

    let botConfig = { Funogram.Telegram.Bot.Config.defaultConfig with Token = telegramSettings.Token }

    interface ITelegramClient with
        member this.GetTemporaryFileLink(fileId) = async {
            let logger = ctx.Logger
            logger.Information("Querying file information for file {FileId}", fileId)
            let! file = Funogram.sendGetFile botConfig fileId
            match file.FilePath with
            | None ->
                logger.Warning("File {FileId} was not found on server", fileId)
                return None
            | Some fp ->
                let uri = Uri $"https://api.telegram.org/file/bot{telegramSettings.Token}/{fp}"
                return! fileCache.DownloadLink uri
        }

    override _.RunUntilError receiver = async {
        // Run loop of Telegram is in no need of any complicated start, so just return an async that will perform it:
        return Funogram.run ctx.Logger telegramSettings databaseSettings hostingSettings botConfig receiver
    }

    override _.Send message =
        Funogram.sendMessage telegramSettings botConfig message
