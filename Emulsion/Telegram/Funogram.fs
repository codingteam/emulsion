module Emulsion.Telegram.Funogram

open System
open System.Text

open Funogram.Telegram
open Funogram.Telegram.Types
open Funogram.Api
open Funogram.Types
open Serilog

open System.Text
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

    let private getUserDisplayName (user: User) =
        match user with
        | { Username = Some username } -> sprintf "@%s" username
        | { LastName = Some lastName } -> sprintf "%s %s" user.FirstName lastName
        | _ -> user.FirstName

    let private getOptionalUserDisplayName: User option -> string = function
    | Some user -> getUserDisplayName user
    | None -> "[UNKNOWN USER]"

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

    let private getQuotedMessage (quoteSettings: QuoteSettings) originalMessage =
        let addAuthorIfAvailable text =
            match originalMessage with
            | Authored msg -> sprintf "<%s> %s" msg.author text
            | Event _ -> text

        let markAsQuote prefix (text: string) =
            text.Split("\n")
            |> Seq.map (fun x -> prefix + x)
            |> String.concat "\n"

        originalMessage
        |> Message.text
        |> applyLimits quoteSettings.limits
        |> addAuthorIfAvailable
        |> markAsQuote quoteSettings.linePrefix

    let private getLinkToMessage (message: FunogramMessage) =
        match message with
        | { MessageId = id
            Chat = { Type = SuperGroup
                     Username = Some chatName } } ->
            sprintf "https://t.me/%s/%d" chatName id
        | _ -> "[DATA UNRECOGNIZED]"

    let private getAuthoredMessageBodyText (message: FunogramMessage) =
        let text =
            match message with
            | { Text = Some text } -> applyEntities message.Entities text
            | { Photo = Some _; Caption = Some caption} ->
                let caption = applyEntities message.CaptionEntities caption
                sprintf "[Photo with caption \"%s\"]: %s" caption (getLinkToMessage message)
            | { Photo = Some _ } ->
                sprintf "[Photo]: %s" (getLinkToMessage message)
            | { Caption = Some caption } ->
                let caption = applyEntities message.CaptionEntities caption
                sprintf "[Content with caption \"%s\"]" caption
            | { Sticker = Some sticker } ->
                let emoji = getEmoji sticker
                sprintf "[Sticker %s]: %s" emoji (getLinkToMessage message)
            | { Poll = Some poll } ->
                let text = getPollText poll
                sprintf "[Poll] %s" text
            | _ -> "[DATA UNRECOGNIZED]"

        if Option.isSome message.ForwardFrom
        then
            let forwardFrom =
                Authored
                    { author = getOptionalUserDisplayName message.ForwardFrom
                      text = text }
            getQuotedMessage DefaultQuoteSettings forwardFrom
        else text

    let private getEventMessageBodyText (message: FunogramMessage) =
        match message with
        | { From = Some originalUser; NewChatMembers = Some users } ->
            let users = Seq.toArray users
            match users with
            | [| user |] when user = originalUser -> sprintf "%s has entered the chat" (getUserDisplayName user)
            | [| user |] -> sprintf "%s has added %s to the chat" (getUserDisplayName originalUser) (getUserDisplayName user)
            | [| user1; user2 |] ->
                sprintf "%s has added %s and %s to the chat"
                    (getUserDisplayName originalUser)
                    (getUserDisplayName user1) (getUserDisplayName user2)
            | _ ->
                let builder = StringBuilder().AppendFormat("{0} has added ", getUserDisplayName originalUser)
                for i = 0 to users.Length - 2 do
                    builder.AppendFormat("{0}, ", getUserDisplayName users.[i]) |> ignore
                builder
                    .AppendFormat("and {0} to the chat", getUserDisplayName users.[users.Length - 1])
                    .ToString()
        | { From = Some originalUser; LeftChatMember = Some user } ->
            if originalUser = user
            then sprintf "%s has left the chat" (getUserDisplayName user)
            else sprintf "%s has removed %s from the chat" (getUserDisplayName originalUser) (getUserDisplayName user)
        | _ -> "[DATA UNRECOGNIZED]"

    let private addOriginalMessage quoteSettings originalMessage replyMessageBody =
        sprintf "%s\n%s" (getQuotedMessage quoteSettings originalMessage) replyMessageBody

    let internal flatten (quotedLimits: QuoteSettings) (message: TelegramMessage): Message =
        match message.main with
        | Authored msg ->
            let author = msg.author
            let mainText = msg.text
            let text =
                match message.replyTo with
                | Some replyTo ->
                    addOriginalMessage quotedLimits replyTo mainText
                | _ -> mainText
            Authored { author = author; text = text }
        | Event _ as msg -> msg

    let private isSelfMessage selfUserId (message: FunogramMessage) =
        match message.From with
        | Some user -> user.Id = selfUserId
        | None -> false

    let private (|EventFunogramMessage|_|) (message: FunogramMessage) =
        if message.NewChatMembers.IsSome || message.LeftChatMember.IsSome
        then Some message
        else None

    let private extractMessageContent(message: FunogramMessage): Message =
        match message with
        | EventFunogramMessage msg ->
            Event { text = getEventMessageBodyText msg }
        | _ ->
            let mainAuthor = getOptionalUserDisplayName message.From
            let mainBody = getAuthoredMessageBodyText message
            Authored { author = mainAuthor; text = mainBody }

    /// For messages from the bot, the first bold section of the message will contain the nickname of the author.
    /// Everything else is the message text.
    let private extractSelfMessageContent(message: FunogramMessage): Message =
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
                Authored { author = authorName; text = messageText }

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

let internal prepareHtmlMessage: Message -> string = function
| Authored {author = author; text = text} -> sprintf "<b>%s</b>\n%s" (Html.escape author) (Html.escape text)
| Event {text = text} -> sprintf "%s" (Html.escape text)

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
