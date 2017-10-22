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

let private defaultText = """⭐️Available test commands:
/send_message1 - Markdown test
/send_message2 - HTML test
/send_message3 - Disable web page preview and notifications
/send_message4 - Test reply message
/send_message5 - Test ReplyKeyboardMarkup
/send_message6 - Test RemoveKeyboardMarkup
/forward_message - Test forward message
/show_my_photos_sizes - Test getUserProfilePhotos method
/get_chat_info - Returns id and type of current chat"""

let private processResult (result: Result<'a, ApiResponseError>) =
    processResultWithValue result |> ignore

let private updateArrived onMessage (ctx : UpdateContext) =
    let bot data = api ctx.Config.Token data |> Async.RunSynchronously |> processResult
    let botResult data = api ctx.Config.Token data |> Async.RunSynchronously

    let getChatInfo msg =
        let result = botResult (getChat msg.Chat.Id)
        match result with
        | Ok x ->
            botResult (sendMessage msg.Chat.Id (sprintf "Id: %i, Type: %s" x.Id x.Type))
            |> processResultWithValue
            |> ignore
        | Error e -> printf "Error: %s" e.Description

    let fromId() = ctx.Update.Message.Value.From.Value.Id

    let sayWithArgs text parseMode disableWebPagePreview disableNotification replyToMessageId replyMarkup =
        bot (sendMessageBase (ChatId.Int (fromId())) text parseMode disableWebPagePreview disableNotification replyToMessageId replyMarkup)

    let sendMessageFormatted text parseMode = bot (sendMessageBase (ChatId.Int(fromId())) text (Some parseMode) None None None None)

    let result =
        processCommands ctx [
            cmd "/send_message1" (fun _ -> sendMessageFormatted "Test *Markdown*" ParseMode.Markdown)
            cmd "/send_message2" (fun _ -> sendMessageFormatted "Test <b>HTML</b>" ParseMode.HTML)
            cmd "/send_message3" (fun _ -> sayWithArgs "@Dolfik! See http://fsharplang.ru - Russian F# Community" None (Some true) (Some true) None None)
            cmd "/send_message4" (fun _ -> sayWithArgs "That's message with reply!" None None None (Some ctx.Update.Message.Value.MessageId) None)
            cmd "/send_message5" (fun _ ->
            (
                let keyboard = (Seq.init 2 (fun x -> Seq.init 2 (fun y -> { Text = y.ToString() + x.ToString(); RequestContact = None; RequestLocation = None })))
                let markup = Markup.ReplyKeyboardMarkup {
                    Keyboard = keyboard
                    ResizeKeyboard = None
                    OneTimeKeyboard = None
                    Selective = None
                }
                bot (sendMessageMarkup (fromId()) "That's keyboard!" markup)
            ))
            cmd "/send_message6" (fun _ ->
            (
                let markup = Markup.ReplyKeyboardRemove { RemoveKeyboard = true; Selective = None; }
                bot (sendMessageMarkup (fromId()) "Keyboard was removed!" markup)
            ))
            cmd "/forward_message" (fun _ -> bot (forwardMessage (fromId()) (fromId()) ctx.Update.Message.Value.MessageId))
            cmd "/show_my_photos_sizes" (fun _ ->
            (
                let x = botResult (getUserProfilePhotosAll (fromId())) |> processResultWithValue
                if x.IsNone then ()
                else
                    let text = sprintf "Photos: %s" (x.Value.Photos
                                |> Seq.map (fun f -> f |> Seq.last)
                                |> Seq.map (fun f -> sprintf "%ix%i" f.Width f.Height)
                                |> String.concat ",")

                    bot (sendMessage (fromId()) text)
            ))
            cmd "/get_chat_info" (fun _ -> getChatInfo ctx.Update.Message.Value)
            fun (msg, user) -> onMessage msg.Text.Value; true
        ]

    if result then ()
    else bot (sendMessage (fromId()) defaultText)

let send (settings : TelegramSettings) (message : string) : unit =
    api settings.token (sendMessage (int64 settings.groupId) message)
    |> Async.RunSynchronously
    |> processResult

let run (settings : TelegramSettings) (onMessage : string -> unit) : unit =
    let config = { defaultConfig with Token = settings.token }
    Bot.startBot config (updateArrived onMessage) None

type TelegramModule =
    { run : TelegramSettings -> (string -> unit) -> unit
      send : TelegramSettings -> string -> unit }

let telegramModule : TelegramModule =
    { run = run
      send = send }
