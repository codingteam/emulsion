namespace Emulsion.Telegram

open Emulsion
open Emulsion.Settings

type Client =
    { run : TelegramSettings -> (Emulsion.Message -> unit) -> unit
      send : TelegramSettings -> OutgoingMessage -> unit }

    with
        static member funogram : Client =
            { run = Funogram.run
              send = Funogram.send }
