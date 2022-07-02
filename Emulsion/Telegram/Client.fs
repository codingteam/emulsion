namespace Emulsion.Telegram

open System.Threading

open Emulsion.Database
open Emulsion.MessageSystem
open Emulsion.Settings

type Client(ctx: ServiceContext,
            cancellationToken: CancellationToken,
            telegramSettings: TelegramSettings,
            databaseSettings: DatabaseSettings option,
            hostingSettings: HostingSettings option) =
    inherit MessageSystemBase(ctx, cancellationToken)

    let botConfig = { Funogram.Telegram.Bot.Config.defaultConfig with Token = telegramSettings.Token }

    override _.RunUntilError receiver = async {
        // Run loop of Telegram is in no need of any complicated start, so just return an async that will perform it:
        return Funogram.run ctx.Logger telegramSettings databaseSettings hostingSettings botConfig receiver
    }

    override _.Send message =
        Funogram.send telegramSettings botConfig message
