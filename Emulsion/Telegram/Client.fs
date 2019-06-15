namespace Emulsion.Telegram

open System.Threading

open Emulsion.MessageSystem
open Emulsion.Settings

type Client(restartContext: RestartContext, cancellationToken: CancellationToken, settings: TelegramSettings) =
    inherit MessageSystemBase(restartContext, cancellationToken)

    override __.RunOnce receiver =
        Funogram.run settings cancellationToken receiver

    override __.Send message =
        Funogram.send settings message
