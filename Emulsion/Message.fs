namespace Emulsion

[<Struct>]
type AuthoredMessage = {
    author: string
    text: string
}

[<Struct>]
type EventMessage = {
    text: string
}

type Message =
| Authored of AuthoredMessage
| Event of EventMessage

type OutgoingMessage =
| OutgoingMessage of Message

type TelegramMessage = {
    main: Message
    replyTo: Message option
}

type IncomingMessage =
| XmppMessage of Message
| TelegramMessage of TelegramMessage
