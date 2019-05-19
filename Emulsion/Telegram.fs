module Emulsion.Telegram

open Funogram
open Funogram.Bot
open Funogram.Api
open Funogram.Types

open Emulsion.Settings

let private processResultWithValue (result: Result<'a, ApiResponseError>) =
    match result with
    | Ok v -> Some v
    | Error e ->
        printfn "Error: %s" e.Description
        None

let private processResult (result: Result<'a, ApiResponseError>) =
    processResultWithValue result |> ignore

let private updateArrived onMessage (ctx : UpdateContext) =
    processCommands ctx [
        fun (msg, _) -> onMessage msg.Text.Value; true
    ] |> ignore

let send (settings : TelegramSettings) (OutgoingMessage(author, text)) : unit =
    let message = sprintf "<%s> %s" author text
    api settings.token (sendMessage (int64 settings.groupId) message)
    |> Async.RunSynchronously
    |> processResult

let run (settings : TelegramSettings) (onMessage : string -> unit) : unit =
    let config = { defaultConfig with Token = settings.token }
    Bot.startBot config (updateArrived onMessage) None

type TelegramModule =
    { run : TelegramSettings -> (string -> unit) -> unit
      send : TelegramSettings -> OutgoingMessage -> unit }

let telegramModule : TelegramModule =
    { run = run
      send = send }
