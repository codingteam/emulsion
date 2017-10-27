namespace Emulsion

type OutgoingMessage =
| OutgoingMessage of author : string * text : string

type IncomingMessage =
| XmppMessage of author : string * text : string
| TelegramMessage of string
with
    member this.toOutgoing() =
        match this with
        | XmppMessage(author, text) -> OutgoingMessage(author, text)
        | TelegramMessage text -> OutgoingMessage("Telegram user", text)
