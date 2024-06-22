// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

namespace Emulsion.Xmpp

open System
open System.Xml.Linq

open SharpXMPP.XMPP

type Presence = {
    From: string
    States: int[]
    Error: XElement option
    Type: string option
}

type RoomInfo = {
    RoomJid: JID
    Nickname: string
    Password: string option
    Ping: {| Interval: TimeSpan option
             Timeout: TimeSpan |}
}

type MessageInfo = {
    RecipientJid: JID
    Text: string
}

type MessageDeliveryInfo = {
    MessageId: string

    /// Resolves after the message is guaranteed to be delivered to the recipient.
    Delivery: Async<unit>
}
