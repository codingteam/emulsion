module Emulsion.Telegram

open Funogram
open Funogram.Bot

let run (token : string) : unit =
    let config = { defaultConfig with Token = token }
    Bot.startBot config (Router.updateArrived token) None
