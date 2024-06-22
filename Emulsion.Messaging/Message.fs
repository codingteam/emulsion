// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

namespace Emulsion.Messaging

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

module Message =
    let text = function
    | Authored msg -> msg.text
    | Event msg -> msg.text

type OutgoingMessage =
| OutgoingMessage of Message

type IncomingMessage =
| XmppMessage of Message
| TelegramMessage of Message
