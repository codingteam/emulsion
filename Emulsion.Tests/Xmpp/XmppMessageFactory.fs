// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

namespace Emulsion.Tests.Xmpp

open System.Xml.Linq
open SharpXMPP.XMPP.Client.Elements

open Emulsion.Xmpp.SharpXmppHelper.Attributes
open Emulsion.Xmpp.SharpXmppHelper.Elements

type XmppMessageFactory =
    static member create(?senderJid: string, ?text: string, ?delayDate: string, ?messageType: string): XMPPMessage =
        let element = XMPPMessage()
        senderJid |> Option.iter (fun from ->
            element.SetAttributeValue(From, from)
        )
        text |> Option.iter (fun t ->
            element.Text <- t
        )
        delayDate |> Option.iter (fun date ->
            let delay = XElement(Delay)
            delay.SetAttributeValue(Stamp, date)
            element.Add(delay)
        )
        messageType |> Option.iter (fun mt ->
            element.SetAttributeValue(Type, mt)
        )

        element
