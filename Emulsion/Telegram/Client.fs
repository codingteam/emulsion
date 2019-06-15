namespace Emulsion.Telegram

open Emulsion.MessageSystem
open Emulsion.Settings

type Client(restartContext: RestartContext, settings: TelegramSettings) =
    inherit MessageSystemBase(restartContext)

    override __.Run receiver _ =
        // TODO[F]: Update Funogram and don't ignore the cancellation token here.
        Funogram.run settings receiver

    override __.Send message =
        Funogram.send settings message
