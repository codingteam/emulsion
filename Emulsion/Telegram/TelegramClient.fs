module Emulsion.Telegram.TelegramClient

open Funogram
open Funogram.Bot
open Funogram.Api
open Funogram.Types

open Emulsion
open Emulsion.Settings

let private processResultWithValue (result: Result<'a, ApiResponseError>) =
    match result with
    | Ok v -> Some v
    | Error e ->
        printfn "Error: %s" e.Description
        None

let private processResult (result: Result<'a, ApiResponseError>) =
    processResultWithValue result |> ignore

let internal convertMessage (message : Funogram.Types.Message) =
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

let internal prepareHtmlMessage { author = author; text = text } : string =
    sprintf "<b>%s</b>\n%s" (Html.escape author) (Html.escape text)

let send (settings : TelegramSettings) (OutgoingMessage content) : unit =
    let sendHtmlMessage groupId text =
        sendMessageBase groupId text (Some ParseMode.HTML) None None None None

    let groupId = Int (int64 settings.groupId)
    let message = prepareHtmlMessage content
    api settings.token (sendHtmlMessage groupId message)
    |> Async.RunSynchronously
    |> processResult

let run (settings : TelegramSettings) (onMessage : Emulsion.Message -> unit) : unit =
    let config = { defaultConfig with Token = settings.token }
    Bot.startBot config (updateArrived onMessage) None
