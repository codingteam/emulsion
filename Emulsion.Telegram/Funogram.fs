module Emulsion.Telegram.Funogram

open System
open System.Text

open Funogram.Telegram
open Funogram.Telegram.Types
open Funogram.Api
open Funogram.Types
open Serilog

open Emulsion.Messaging
open Emulsion.Database
open Emulsion.Settings
open Emulsion.Telegram.LinkGenerator

type private FunogramMessage = Types.Message
[<Struct>]
type internal ThreadMessage = {
    main: Message
    replyTo: Message option
}

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

    let [<Literal>] DefaultMessageLengthLimit = 500
    let [<Literal>] DefaultMessageLinesLimit = 3

    let DefaultQuoteSettings = {
        limits = {
            messageLengthLimit = Some DefaultMessageLengthLimit
            messageLinesLimit = Some DefaultMessageLinesLimit
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
        | { Username = Some username } -> $"@{username}"
        | { LastName = Some lastName } -> $"{user.FirstName} {lastName}"
        | _ -> user.FirstName

    let private getChatDisplayName (chat: Chat) =
        match chat with
        | { Username = Some username } -> $"@{username}"
        | { Title = Some title } -> title
        | _ -> "[UNKNOWN CHAT]"

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
            | Authored msg -> $"<{msg.author}> {text}"
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

    let private getAuthoredMessageBodyText (message: FunogramMessage) links =
        let (|Text|_|) (message: FunogramMessage) = message.Text
        let (|Poll|_|) (message: FunogramMessage) = message.Poll
        let (|Content|_|) (message: FunogramMessage) =
            let contentType =
                match message with
                | { Photo = Some _ } -> "Photo"
                | { Animation = Some _ } -> "Animation"
                | { Sticker = Some sticker } -> $"Sticker {getEmoji sticker}"
                | { Caption = Some _ } -> "Unknown content"
                | _ -> String.Empty
            if String.IsNullOrEmpty contentType
            then None
            else Some (contentType, message.Caption)

        let (|ForwardFrom|_|) (message: FunogramMessage) =
            match message with
            | { ForwardFrom = Some user } -> Some (getUserDisplayName user)
            | { ForwardSenderName = Some sender } -> Some sender
            | { ForwardFromChat = Some chat } -> Some (getChatDisplayName chat)
            | _ -> None

        let appendLinkTo text =
            let addedLinks =
                links
                |> Seq.map(fun uri -> uri.ToString())
                |> String.concat "\n"
            match addedLinks with
            | "" -> text
            | _ -> $"{text}: {addedLinks}"

        let text =
            match message with
            | Text text -> applyEntities message.Entities text
            | Content (contentType, caption) ->
                let contentInfo =
                    match caption with
                    | Some caption ->
                        let text = applyEntities message.CaptionEntities caption
                        $"[{contentType} with caption \"{text}\"]"
                    | None ->
                        $"[{contentType}]"
                appendLinkTo contentInfo
            | Poll poll ->
                let text = getPollText poll
                $"[Poll] {text}"
            | _ ->
                appendLinkTo "[DATA UNRECOGNIZED]"

        match message with
        | ForwardFrom author ->
            let forwardFrom = Authored { author = author; text = text }
            getQuotedMessage { DefaultQuoteSettings with limits = Unlimited } forwardFrom
        | _ -> text

    let private getEventMessageBodyText (message: FunogramMessage) =
        let name = getUserDisplayName
        match message with
        | { From = Some originalUser; NewChatMembers = Some users } ->
            let users = Seq.toArray users
            match users with
            | [| user |] when user = originalUser -> $"{name user} has entered the chat"
            | [| user |] -> $"{name originalUser} has added {name user} to the chat"
            | [| user1; user2 |] ->
                $"{name originalUser} has added {name user1} and {name user2} to the chat"
            | _ ->
                let builder = StringBuilder().AppendFormat("{0} has added ", getUserDisplayName originalUser)
                for i = 0 to users.Length - 2 do
                    builder.AppendFormat("{0}, ", getUserDisplayName users[i]) |> ignore
                builder
                    .AppendFormat("and {0} to the chat", getUserDisplayName users[users.Length - 1])
                    .ToString()
        | { From = Some originalUser; LeftChatMember = Some user } ->
            if originalUser = user
            then $"{name user} has left the chat"
            else $"{name originalUser} has removed {name user} from the chat"
        | _ -> "[DATA UNRECOGNIZED]"

    let private addOriginalMessage quoteSettings originalMessage replyMessageBody =
        let quotedMessage = getQuotedMessage quoteSettings originalMessage
        $"{quotedMessage}\n\n{replyMessageBody}"

    let internal flatten (quotedLimits: QuoteSettings) (message: ThreadMessage): Message =
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

    let private extractMessageContent(message: FunogramMessage) links: Message =
        match message with
        | EventFunogramMessage msg ->
            Event { text = getEventMessageBodyText msg }
        | _ ->
            let mainAuthor =
                message.From
                |> Option.map getUserDisplayName
                |> Option.defaultValue "[UNKNOWN USER]"
            let mainBody = getAuthoredMessageBodyText message links
            Authored { author = mainAuthor; text = mainBody }

    /// For messages from the bot, the first bold section of the message will contain the nickname of the author.
    /// Everything else is the message text.
    let private extractSelfMessageContent(message: FunogramMessage) link: Message =
        match (message.Entities, message.Text) with
        | None, _ | _, None -> extractMessageContent message link
        | Some entities, Some text ->
            let boldEntity = Seq.tryFind (fun (e: MessageEntity) -> e.Type = "bold") entities
            match boldEntity with
            | None -> extractMessageContent message link
            | Some section ->
                let authorNameOffset = Math.Clamp(int32 section.Offset, 0, text.Length)
                let authorNameLength = Math.Clamp(int32 section.Length, 0, text.Length - authorNameOffset)
                let authorName = text.Substring(authorNameOffset, authorNameLength)
                let messageTextOffset = Math.Clamp(authorNameOffset + authorNameLength + 1, 0, text.Length) // +1 for \n
                let messageText = text.Substring messageTextOffset
                Authored { author = authorName; text = messageText }

    let (|ForumTopicCreatedMessage|_|) (m: FunogramMessage option) =
        match m with
        | Some m when Option.isSome m.ForumTopicCreated -> Some m
        | _ -> None

    let internal read (selfUserId: int64) (message: FunogramMessage, links: TelegramThreadLinks): ThreadMessage =
        let mainMessage = extractMessageContent message links.ContentLinks
        match message.ReplyToMessage with
        | None | ForumTopicCreatedMessage _ -> { main = mainMessage; replyTo = None }
        | Some replyTo ->
            let replyToMessage =
                if isSelfMessage selfUserId replyTo
                then extractSelfMessageContent replyTo links.ReplyToContentLinks
                else extractMessageContent replyTo links.ReplyToContentLinks
            { main = mainMessage; replyTo = Some replyToMessage }

let internal processSendResult(result: Result<'a, ApiResponseError>): 'a =
    match result with
    | Ok x -> x
    | Error e ->
        failwith $"Telegram API Call processing error {e.ErrorCode}: {e.Description}"

let private extractLinkData logger databaseSettings hostingSettings message =
    message, Async.RunSynchronously(gatherLinks logger databaseSettings hostingSettings message)

let internal processMessage (logger: ILogger)
                            (databaseSettings: DatabaseSettings option)
                            (hostingSettings: HostingSettings option)
                            (context: {| SelfUserId: int64; GroupId: int64; MessageThreadId: int64 option |})
                            (message: FunogramMessage): Message option =
    let correctGroup = context.GroupId = message.Chat.Id
    let correctThread =
        match context.MessageThreadId with
        | None -> true
        | _ -> message.MessageThreadId = context.MessageThreadId
    if correctGroup && correctThread
    then
        message
        |> extractLinkData logger databaseSettings hostingSettings
        |> MessageConverter.read context.SelfUserId
        |> MessageConverter.flatten MessageConverter.DefaultQuoteSettings
        |> Some
    else None

let private updateArrived databaseSettings
                          hostingSettings
                          groupId
                          messageThreadId
                          (logger: ILogger)
                          onMessage
                          (ctx: Bot.UpdateContext) =
    let readContext = {|
        SelfUserId = ctx.Me.Id
        GroupId = groupId
        MessageThreadId = messageThreadId
    |}
    Bot.processCommands ctx [
        fun ctx ->
            match ctx.Update.Message with
            | Some msg ->
                logger.Information("Incoming Telegram message: {Message}", msg)
                match processMessage logger databaseSettings hostingSettings readContext msg with
                | Some m -> onMessage(TelegramMessage m)
                | None -> ()
                true
            | _ -> false
    ] |> ignore

let internal prepareHtmlMessage: Message -> string = function
| Authored {author = author; text = text} -> $"<b>{Html.escape author}</b>\n{Html.escape text}"
| Event {text = text} -> Html.escape text

let private send (botConfig: BotConfig) request = api botConfig request

let sendGetFile (botConfig: BotConfig) (fileId: string): Async<File> = async {
    let! result = send botConfig (Req.GetFile.Make fileId)
    return processSendResult result
}

let sendMessage (settings: TelegramSettings) (botConfig: BotConfig) (OutgoingMessage content): Async<unit> =
    let groupId = Int(int64 settings.GroupId)
    let threadId = settings.MessageThreadId
    let sendHtmlMessage (groupId: ChatId) text =
        Req.SendMessage.Make(chatId = groupId, text = text, ?messageThreadId = threadId, parseMode = ParseMode.HTML)

    let message = prepareHtmlMessage content
    async {
        let! result = send botConfig (sendHtmlMessage groupId message)
        processSendResult result |> ignore
        return ()
    }

let run (logger: ILogger)
        (telegramSettings: TelegramSettings)
        (databaseSettings: DatabaseSettings option)
        (hostingSettings: HostingSettings option)
        (botConfig: BotConfig)
        (onMessage: IncomingMessage -> unit): Async<unit> =
    Bot.startBot botConfig
                 (updateArrived databaseSettings
                                hostingSettings
                                telegramSettings.GroupId
                                telegramSettings.MessageThreadId
                                logger
                                onMessage)
                 None
