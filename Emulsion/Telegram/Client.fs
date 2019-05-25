namespace Emulsion.Telegram

open Emulsion
open Emulsion.Settings

type Client =
    { run : (Emulsion.Message -> unit) -> unit
      send : OutgoingMessage -> unit }

    with
        static member funogram (settings : TelegramSettings) : Client =
            { run = Funogram.run settings
              send = Funogram.send settings }
