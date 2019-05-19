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

let internal convertMessage (message : Message) =
    let authorName =
        match message.From with
        | None -> "[UNKNOWN USER]"
        | Some user ->
            match user.Username with
            | Some username -> sprintf "@%s" username
            | None ->
                match user.LastName with
                | Some lastName -> sprintf "%s %s" user.FirstName lastName
                | None -> user.FirstName
    let text = Option.defaultValue "[DATA UNRECOGNIZED]" message.Text
    { author = authorName; text = text }

let private updateArrived onMessage (ctx : UpdateContext) =
    processCommands ctx [
        fun (msg, _) -> onMessage (convertMessage msg); true
    ] |> ignore

let send (settings : TelegramSettings) (OutgoingMessage { author = author; text = text }) : unit =
    let message = sprintf "<%s> %s" author text
    api settings.token (sendMessage (int64 settings.groupId) message)
    |> Async.RunSynchronously
    |> processResult

let run (settings : TelegramSettings) (onMessage : Emulsion.Message -> unit) : unit =
    let config = { defaultConfig with Token = settings.token }
    Bot.startBot config (updateArrived onMessage) None

type TelegramModule =
    { run : TelegramSettings -> (Emulsion.Message -> unit) -> unit
      send : TelegramSettings -> OutgoingMessage -> unit }

let telegramModule : TelegramModule =
    { run = run
      send = send }
