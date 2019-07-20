module Emulsion.Telegram.Funogram

open System.Threading

open Funogram
open Funogram.Bot
open Funogram.Api
open Funogram.Types

open Emulsion
open Emulsion.Settings

type private FunogramMessage = Funogram.Types.Message

module MessageConverter =
    type MessageLimits = {
        messageLengthLimit: int option
        messageLinesLimit: int option
        dataRedactedMessage: string
    }

    type QuoteSettings = {
        limits: MessageLimits
        linePrefix: string
    }

    let DefaultQuoteSettings = {
        limits = {
            messageLengthLimit = Some 500
            messageLinesLimit = Some 3
            dataRedactedMessage = "[…]"
        }
        linePrefix = ">> "
    }

    let private Unlimited = {
        messageLengthLimit = None
        messageLinesLimit = None
        dataRedactedMessage = ""
    }

    let private getAuthorName (message: FunogramMessage) =
        match message.From with
        | None -> "[UNKNOWN USER]"
        | Some user ->
            match user.Username with
            | Some username -> sprintf "@%s" username
            | None ->
                match user.LastName with
                | Some lastName -> sprintf "%s %s" user.FirstName lastName
                | None -> user.FirstName

    let private getMessageBodyText (message: FunogramMessage) =
        match message.Text with
        | None -> "[DATA UNRECOGNIZED]"
        | Some text -> text

    let private applyLimits limits text =
        let applyMessageLengthLimit (original: {| text: string; wasLimited: bool |}) =
            match limits.messageLengthLimit with
            | None -> original
            | Some limit when original.text.Length <= limit -> original
            | Some limit ->
                let newText = original.text.Substring(0, max 0 (limit - limits.dataRedactedMessage.Length))
                {| text = newText; wasLimited = true |}

        let applyLineLimit (original: {| text: string; wasLimited: bool |}) =
            match limits.messageLinesLimit with
            | None -> original
            | Some limit ->
                let lines = original.text.Split("\n")
                if lines.Length > limit
                then
                    let newText =
                        (lines
                         |> Seq.take(limit - 1)
                         |> String.concat "\n"
                        ) + "\n" // to add the "DATA REDACTED" message onto a new line
                    {| text = newText; wasLimited = true |}
                else original

        let result =
            {| text = text; wasLimited = false |}
            |> applyMessageLengthLimit
            |> applyLineLimit
        if result.wasLimited
        then result.text + limits.dataRedactedMessage
        else result.text

    let private addOriginalMessage quoteSettings originalMessage replyMessageBody =
        let markAsQuote (text: string) =
            text.Split("\n")
            |> Seq.map (fun x -> quoteSettings.linePrefix + x)
            |> String.concat "\n"

        let originalAuthorName = originalMessage.author
        let originalMessageBody =
            originalMessage.text
            |> applyLimits quoteSettings.limits

        (sprintf "<%s> %s" originalAuthorName originalMessageBody
         |> markAsQuote) + "\n" + replyMessageBody

    let internal flatten (quotedLimits: QuoteSettings) (message: TelegramMessage): Message =
        let author = message.main.author
        let mainText = message.main.text
        let text =
            match message.replyTo with
            | None -> mainText
            | Some replyTo ->
                addOriginalMessage quotedLimits replyTo mainText

        { author = author; text = text }

    let internal read (message: FunogramMessage): TelegramMessage =
        let mainAuthor = getAuthorName message
        let mainBody = getMessageBodyText message
        let mainMessage = { author = mainAuthor; text = mainBody }

        match message.ReplyToMessage with
        | None -> { main = mainMessage; replyTo = None }
        | Some replyTo ->
            let replyToMessage = { author = getAuthorName replyTo; text = getMessageBodyText replyTo }
            { main = mainMessage; replyTo = Some replyToMessage }

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
        fun (msg, _) -> onMessage (TelegramMessage (MessageConverter.read msg)); true
    ] |> ignore

let internal prepareHtmlMessage { author = author; text = text } : string =
    sprintf "<b>%s</b>\n%s" (Html.escape author) (Html.escape text)

let send (settings : TelegramSettings) (OutgoingMessage content) : Async<unit> =
    let sendHtmlMessage groupId text =
        sendMessageBase groupId text (Some ParseMode.HTML) None None None None

    let groupId = Int (int64 settings.GroupId)
    let message = prepareHtmlMessage content
    async {
        let! result = api settings.Token (sendHtmlMessage groupId message)
        return processResult result
    }

let run (settings: TelegramSettings)
        (cancellationToken: CancellationToken)
        (onMessage: IncomingMessage -> unit) : unit =
    // TODO[F]: Update Funogram and don't ignore the cancellation token here.
    let config = { defaultConfig with Token = settings.Token }
    Bot.startBot config (updateArrived onMessage) None
