namespace Emulsion

[<Struct>]
type Message = {
    author : string
    text : string
}

type OutgoingMessage =
| OutgoingMessage of Message

type IncomingMessage =
| XmppMessage of Message
| TelegramMessage of Message
