namespace Emulsion.Telegram

open System.Threading

open Emulsion.MessageSystem
open Emulsion.Settings

type Client(ctx: RestartContext, cancellationToken: CancellationToken, settings: TelegramSettings) =
    inherit MessageSystemBase(ctx, cancellationToken)

    override __.RunUntilError receiver =
        Funogram.run settings cancellationToken receiver

    override __.Send message =
        Funogram.send settings message
