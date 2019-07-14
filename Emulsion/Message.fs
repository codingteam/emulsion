namespace Emulsion

[<Struct>]
type Message = {
    author : string
    text : string
}

type OutgoingMessage =
| OutgoingMessage of Message

type TelegramMessage = {
    main: Message
    replyTo: Message option
}

type IncomingMessage =
| XmppMessage of Message
| TelegramMessage of TelegramMessage
