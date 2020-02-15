namespace Emulsion.Telegram

open System.Threading

open Emulsion.MessageSystem
open Emulsion.Settings

type Client(ctx: ServiceContext, cancellationToken: CancellationToken, settings: TelegramSettings) =
    inherit MessageSystemBase(ctx, cancellationToken)

    let botConfig = { Funogram.Telegram.Bot.defaultConfig with Token = settings.Token }

    override _.RunUntilError receiver = async {
        // Run loop of Telegram is in no need for complicated start, so just return an async that will perform it:
        return Funogram.run ctx.Logger settings botConfig receiver
    }

    override _.Send message =
        Funogram.send ctx.Logger settings botConfig message
