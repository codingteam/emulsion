module Emulsion.Telegram.Module

open Emulsion
open Emulsion.Settings

type TelegramModule =
    { run : TelegramSettings -> (Emulsion.Message -> unit) -> unit
      send : TelegramSettings -> OutgoingMessage -> unit }

let telegramModule : TelegramModule =
    { run = TelegramClient.run
      send = TelegramClient.send }
