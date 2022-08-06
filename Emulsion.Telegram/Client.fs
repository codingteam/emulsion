namespace Emulsion.Telegram

open System
open System.Threading

open Emulsion.Database
open Emulsion.Messaging.MessageSystem
open Emulsion.Settings

type ITelegramClient =
    abstract GetTemporaryFileLink: fileId: string -> Async<Uri>

type Client(ctx: ServiceContext,
            cancellationToken: CancellationToken,
            telegramSettings: TelegramSettings,
            databaseSettings: DatabaseSettings option,
            hostingSettings: HostingSettings option) =
    inherit MessageSystemBase(ctx, cancellationToken)

    let botConfig = { Funogram.Telegram.Bot.Config.defaultConfig with Token = telegramSettings.Token }

    interface ITelegramClient with
        member this.GetTemporaryFileLink(fileId) = async {
            let! file = Funogram.sendGetFile botConfig fileId
            return file.FilePath |> Option.map(fun fp -> Uri (failwith "Invalid: public file link is impossible to generate"))
        }

    override _.RunUntilError receiver = async {
        // Run loop of Telegram is in no need of any complicated start, so just return an async that will perform it:
        return Funogram.run ctx.Logger telegramSettings databaseSettings hostingSettings botConfig receiver
    }

    override _.Send message =
        Funogram.sendMessage telegramSettings botConfig message
