module Emulsion.Telegram.Funogram

open System
open System.Text
open System.Threading

open Funogram
open Funogram.Bot
open Funogram.Api
open Funogram.Types
open Serilog

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

    let private getUserDisplayName: User option -> string = function
    | None -> "[UNKNOWN USER]"
    | Some user ->
        match user.Username with
        | Some username -> sprintf "@%s" username
        | None ->
            match user.LastName with
            | Some lastName -> sprintf "%s %s" user.FirstName lastName
            | None -> user.FirstName

    let private getTextLinkEntity = function
    | { Type = "text_link"; Url = Some url; Offset = o; Length = l }
        when o >= 0L
             && l > 0L
             && o < int64 Int32.MaxValue
             && l < int64 Int32.MaxValue
             && o + l < int64 Int32.MaxValue ->
        Some {| Url = url; Offset = o; Length = l |}
    | _ -> None

    let private getEmoji(sticker: Sticker) =
        Option.defaultValue "UNKNOWN" sticker.Emoji

    let private applyEntities entities (text: string) =
        match entities with
        | None -> text
        | Some entities ->
            let links =
                entities
                |> Seq.choose getTextLinkEntity
                |> Seq.sortBy (fun e -> e.Offset)
            let result = StringBuilder()
            let mutable pos = 0
            for link in links do
                let linkEndOffset = min text.Length (int32 (link.Offset + link.Length))
                result
                    .Append(text.Substring(pos, linkEndOffset - pos))
                    .AppendFormat(" [{0}]", link.Url)
                |> ignore
                pos <- linkEndOffset
            result.Append(text.Substring(pos, text.Length - pos)).ToString()

    let private getQuotedMessage (quoteSettings: QuoteSettings) author text =
        let formatWithAuthor author message =
            sprintf "<%s> %s" author message

        let markAsQuote prefix (text: string) =
            text.Split("\n")
            |> Seq.map (fun x -> prefix + x)
            |> String.concat "\n"

        formatWithAuthor author text
        |> markAsQuote quoteSettings.linePrefix

    let private getMessageBodyText (message: FunogramMessage) =
        let text =
            match message.Text with
            | None ->
                match message.Sticker with
                | None -> "[DATA UNRECOGNIZED]"
                | Some sticker ->
                    let emoji = getEmoji sticker
                    sprintf "[Sticker %s]" emoji
            | Some text -> applyEntities message.Entities text

        if Option.isSome message.ForwardFrom
        then getQuotedMessage DefaultQuoteSettings (getUserDisplayName message.ForwardFrom) text
        else text

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
        let originalAuthorName = originalMessage.author
        let originalMessageBody =
            originalMessage.text
            |> applyLimits quoteSettings.limits

        (getQuotedMessage quoteSettings originalAuthorName originalMessageBody) + "\n" + replyMessageBody

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
        let mainAuthor = getUserDisplayName message.From
        let mainBody = getMessageBodyText message
        let mainMessage = { author = mainAuthor; text = mainBody }

        match message.ReplyToMessage with
        | None -> { main = mainMessage; replyTo = None }
        | Some replyTo ->
            let replyToMessage = { author = getUserDisplayName replyTo.From; text = getMessageBodyText replyTo }
            { main = mainMessage; replyTo = Some replyToMessage }

let private processResultWithValue (logger: ILogger) (result: Result<'a, ApiResponseError>) =
    match result with
    | Ok v -> Some v
    | Error e ->
        logger.Error("Telegram API call processing error: {Error}", e)
        None

let private processResult logger (result: Result<'a, ApiResponseError>) =
    processResultWithValue logger result |> ignore

let private updateArrived (logger: ILogger) onMessage (ctx: UpdateContext) =
    processCommands ctx [
        fun (msg, _) ->
            logger.Information("Incoming Telegram message: {Message}", msg)
            onMessage (TelegramMessage(MessageConverter.read msg)); true
    ] |> ignore

let internal prepareHtmlMessage { author = author; text = text }: string =
    sprintf "<b>%s</b>\n%s" (Html.escape author) (Html.escape text)

let send (logger: ILogger) (settings: TelegramSettings) (OutgoingMessage content): Async<unit> =
    let sendHtmlMessage groupId text =
        sendMessageBase groupId text (Some ParseMode.HTML) None None None None

    let groupId = Int(int64 settings.GroupId)
    let message = prepareHtmlMessage content
    async {
        let! result = api settings.Token (sendHtmlMessage groupId message)
        return processResult logger result
    }

let run (logger: ILogger)
        (settings: TelegramSettings)
        (cancellationToken: CancellationToken)
        (onMessage: IncomingMessage -> unit): unit =
    // TODO[F]: Update Funogram and don't ignore the cancellation token here.
    let config = { defaultConfig with Token = settings.Token }
    Bot.startBot config (updateArrived logger onMessage) None
