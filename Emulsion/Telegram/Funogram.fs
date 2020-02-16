module Emulsion.Telegram.Funogram

open System
open System.Text

open Funogram.Telegram
open Funogram.Telegram.Types
open Funogram.Api
open Funogram.Types
open Serilog

open Emulsion
open Emulsion.Settings

type private FunogramMessage = Funogram.Telegram.Types.Message

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

    let private getPollText (poll: Poll) =
        let result = StringBuilder().Append(poll.Question)
        for option in poll.Options do
            result.AppendFormat("\n- {0}", option.Text) |> ignore
        result.ToString()

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
            match message with
            | { Text = Some text } -> applyEntities message.Entities text
            | { Caption = Some caption } ->
                let caption = applyEntities message.CaptionEntities caption
                sprintf "[Content with caption \"%s\"]" caption
            | { Sticker = Some sticker }  ->
                let emoji = getEmoji sticker
                sprintf "[Sticker %s]" emoji
            | { Poll = Some poll } ->
                let text = getPollText poll
                sprintf "[Poll] %s" text
            | _ -> "[DATA UNRECOGNIZED]"

        if Option.isSome message.ForwardFrom
        then getQuotedMessage DefaultQuoteSettings (getUserDisplayName message.ForwardFrom) text
        else text

    let private applyLimits limits text =
        let applyMessageLengthLimit (original: {| text: string; wasLimited: bool |}) =
            match limits.messageLengthLimit with
            | None -> original
            | Some limit when original.text.Length <= limit -> original
            | Some limit ->
                let newText = original.text.Substring(0,
                                                      Math.Clamp(limit - limits.dataRedactedMessage.Length,
                                                                 0,
                                                                 original.text.Length))
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

    let private isSelfMessage selfUserId (message: FunogramMessage) =
        match message.From with
        | Some user -> user.Id = selfUserId
        | None -> false

    let private extractMessageContent(message: FunogramMessage) =
        let mainAuthor = getUserDisplayName message.From
        let mainBody = getMessageBodyText message
        { author = mainAuthor; text = mainBody }

    /// For messages from the bot, the first bold section of the message will contain the nickname of the author.
    /// Everything else is the message text.
    let private extractSelfMessageContent(message: FunogramMessage) =
        match (message.Entities, message.Text) with
        | None, _ | _, None -> extractMessageContent message
        | Some entities, Some text ->
            let boldEntity = Seq.tryFind (fun (e: MessageEntity) -> e.Type = "bold") entities
            match boldEntity with
            | None -> extractMessageContent message
            | Some section ->
                let authorNameOffset = Math.Clamp(int32 section.Offset, 0, text.Length)
                let authorNameLength = Math.Clamp(int32 section.Length, 0, text.Length - authorNameOffset)
                let authorName = text.Substring(authorNameOffset, authorNameLength)
                let messageTextOffset = Math.Clamp(authorNameOffset + authorNameLength + 1, 0, text.Length) // +1 for \n
                let messageText = text.Substring messageTextOffset
                { author = authorName; text = messageText }

    let internal read (selfUserId: int64) (message: FunogramMessage): TelegramMessage =
        let mainMessage = extractMessageContent message
        match message.ReplyToMessage with
        | None -> { main = mainMessage; replyTo = None }
        | Some replyTo ->
            let replyToMessage =
                if isSelfMessage selfUserId replyTo
                then extractSelfMessageContent replyTo
                else extractMessageContent replyTo
            { main = mainMessage; replyTo = Some replyToMessage }

let private processResultWithValue (logger: ILogger) (result: Result<'a, ApiResponseError>) =
    match result with
    | Ok v -> Some v
    | Error e ->
        logger.Error("Telegram API call processing error: {Error}", e)
        None

let private processResult logger (result: Result<'a, ApiResponseError>) =
    processResultWithValue logger result |> ignore

let internal processMessage (context: {| SelfUserId: int64; GroupId: int64 |})
                            (message: FunogramMessage): TelegramMessage option =
    if context.GroupId = message.Chat.Id
    then Some <| MessageConverter.read context.SelfUserId message
    else None

let private updateArrived groupId (logger: ILogger) onMessage (ctx: Bot.UpdateContext) =
    let readContext = {|
        SelfUserId = ctx.Me.Id
        GroupId = groupId
    |}
    Bot.processCommands ctx [
        fun ctx ->
            match ctx.Update.Message with
            | Some msg ->
                logger.Information("Incoming Telegram message: {Message}", msg)
                match processMessage readContext msg with
                | Some m -> onMessage(TelegramMessage m)
                | None -> logger.Warning "Message from unidentified source ignored"
                true
            | _ -> false
    ] |> ignore

let internal prepareHtmlMessage { author = author; text = text }: string =
    sprintf "<b>%s</b>\n%s" (Html.escape author) (Html.escape text)

let send (logger: ILogger) (settings: TelegramSettings) (botConfig: BotConfig) (OutgoingMessage content): Async<unit> =
    let sendHtmlMessage groupId text =
        Api.sendMessageBase groupId text (Some ParseMode.HTML) None None None None

    let groupId = Int(int64 settings.GroupId)
    let message = prepareHtmlMessage content
    async {
        let! result = api botConfig (sendHtmlMessage groupId message)
        return processResult logger result
    }

let run (logger: ILogger)
        (settings: TelegramSettings)
        (botConfig: BotConfig)
        (onMessage: IncomingMessage -> unit): Async<unit> =
    Bot.startBot botConfig (updateArrived settings.GroupId logger onMessage) None
