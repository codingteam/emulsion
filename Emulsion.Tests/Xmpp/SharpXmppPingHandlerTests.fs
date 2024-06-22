// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

module Emulsion.Tests.Xmpp.SharpXmppPingHandlerTests

open System.IO
open System.Xml

open SharpXMPP
open SharpXMPP.XMPP
open SharpXMPP.XMPP.Client.Elements
open Xunit

open Emulsion.Xmpp
open Emulsion.Xmpp.SharpXmppHelper.Attributes

let private handler = SharpXmppPingHandler()
type private MockedXmppTcpConnection() as this =
    inherit XmppTcpConnection("", JID(), "")
    do this.Writer <- XmlWriter.Create Stream.Null

[<Fact>]
let ``SharpXmppPingHandler handles a ping request``() =
    let jid = JID "me@example.com"
    let request = SharpXmppHelper.ping jid "test"
    request.SetAttributeValue(From, "they@example.com")

    use connection = new MockedXmppTcpConnection()
    Assert.True(handler.Handle(connection, request))

[<Fact>]
let ``SharpXmppPingHandler sends a pong response``() =
    let jid = JID "me@example.com"
    let request = SharpXmppHelper.ping jid "test"
    request.SetAttributeValue(From, "they@example.com")

    let elements = ResizeArray()
    use connection = new MockedXmppTcpConnection()
    connection.add_Element(fun _ e -> elements.Add e.Stanza)

    Assert.True(handler.Handle(connection, request))
    let pong = Seq.exactlyOne elements
    Assert.True(SharpXmppHelper.isPong jid "test" (pong :?> XMPPIq))

[<Fact>]
let ``SharpXmppPingHandler ignores a non-ping query``() =
    let elements = ResizeArray()
    use connection = new MockedXmppTcpConnection()
    connection.add_Element(fun _ e -> elements.Add e.Stanza)

    Assert.False(handler.Handle(connection, XMPPIq(XMPPIq.IqTypes.get)))
    Assert.Empty elements
