namespace Emulsion

type Message =
| XmppMessage of string
| TelegramMessage of string
