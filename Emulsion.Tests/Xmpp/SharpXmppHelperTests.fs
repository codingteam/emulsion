// SPDX-FileCopyrightText: 2025 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

module Emulsion.Tests.Xmpp.SharpXmppHelperTests

open System.Xml.Linq

open SharpXMPP.XMPP
open SharpXMPP.XMPP.Client.Elements
open Xunit

open Emulsion.Messaging
open Emulsion.Tests.Xmpp
open Emulsion.Xmpp
open Emulsion.Xmpp.SharpXmppHelper.Attributes
open Emulsion.Xmpp.SharpXmppHelper.Elements

[<Fact>]
let ``SanitizeXmlText processes emoji as-is``(): unit =
    Assert.Equal("🐙", SharpXmppHelper.SanitizeXmlText "🐙")
    Assert.Equal("test🐙", SharpXmppHelper.SanitizeXmlText "test🐙")

[<Fact>]
let ``SanitizeXmlText replaces parts of UTF-16 surrogate pair with the replacement char``(): unit =
    let octopus = "🐙"
    Assert.Equal(2, octopus.Length)
    let firstHalf = string(octopus[0])
    let secondHalf = string(octopus[1])
    Assert.Equal("🐙", firstHalf + secondHalf)
    Assert.Equal("�", SharpXmppHelper.SanitizeXmlText firstHalf)
    Assert.Equal("�", SharpXmppHelper.SanitizeXmlText secondHalf)
    Assert.Equal("test�", SharpXmppHelper.SanitizeXmlText $"test{secondHalf}")

[<Fact>]
let ``Message body has a proper namespace``() =
    let message = SharpXmppHelper.message "" "cthulhu@test" "text"
    let body = Seq.exactlyOne(message.Descendants())
    Assert.Equal(XNamespace.Get "jabber:client", body.Name.Namespace)

[<Fact>]
let ``parseMessage should extract message text and author``() =
    let text = "text test"
    let element = XmppMessageFactory.create("x@y/author", text)
    let message = SharpXmppHelper.parseMessage element
    let expected = Authored { author = "author"; text = text }
    Assert.Equal(expected, message)

[<Fact>]
let ``Message without author is attributed to [UNKNOWN USER]``() =
    let text = "xxx"
    let element = XmppMessageFactory.create(text = text)
    let message = SharpXmppHelper.parseMessage element
    let expected = Authored { author = "[UNKNOWN USER]"; text = text }
    Assert.Equal(expected, message)

[<Fact>]
let ``isOwnMessage detects own message by resource``() =
    let message = XmppMessageFactory.create("a@b/myNickname", "text")
    Assert.True(SharpXmppHelper.isOwnMessage "myNickname" message)

[<Fact>]
let ``isOwnMessage detects foreign message``() =
    let message = XmppMessageFactory.create("a@b/notMyNickname", "text")
    Assert.False(SharpXmppHelper.isOwnMessage "myNickname" message)

[<Fact>]
let ``isOwnMessage detects nobody's message``() =
    let message = XmppMessageFactory.create()
    Assert.False(SharpXmppHelper.isOwnMessage "myNickname" message)

[<Fact>]
let ``isHistoricalMessage returns false for an ordinary message``() =
    let message = XmppMessageFactory.create()
    Assert.False(SharpXmppHelper.isHistoricalMessage message)

[<Fact>]
let ``isHistoricalMessage returns true for a message with delay``() =
    let message = XmppMessageFactory.create(delayDate = "2010-01-01")
    Assert.True(SharpXmppHelper.isHistoricalMessage message)

[<Fact>]
let ``Message without body is considered as empty``(): unit =
    let message = XmppMessageFactory.create()
    Assert.True(SharpXmppHelper.isEmptyMessage message)

[<Fact>]
let ``Message consisting of whitespace is considered as empty``(): unit =
    let message = XmppMessageFactory.create(text = "     \t ")
    Assert.True(SharpXmppHelper.isEmptyMessage message)

[<Fact>]
let ``Message with text is not considered as empty``(): unit =
    let message = XmppMessageFactory.create(text = "     t ")
    Assert.False(SharpXmppHelper.isEmptyMessage message)

[<Fact>]
let ``Message with proper type is a group chat message``(): unit =
    Assert.True(SharpXmppHelper.isGroupChatMessage(XmppMessageFactory.create(messageType = "groupchat")))
    Assert.False(SharpXmppHelper.isGroupChatMessage(XmppMessageFactory.create(messageType = "error")))

[<Fact>]
let ``isPing determines ping IQ query according to the spec``(): unit =
    let jid = JID("room@conference.example.com/me")
    let ping = SharpXmppHelper.ping jid "myTest"
    Assert.True(SharpXmppHelper.isPing ping)

    ping.Element(Ping).Remove()
    Assert.False(SharpXmppHelper.isPing ping)

[<Fact>]
let ``isPong determines pong response according to the spec``(): unit =
    let jid = JID("room@conference.example.com/me")
    let pongResponse = XMPPIq(XMPPIq.IqTypes.result, "myTest")
    pongResponse.SetAttributeValue(From, jid.FullJid)

    Assert.True(SharpXmppHelper.isPong jid "myTest" pongResponse)
    Assert.False(SharpXmppHelper.isPong jid "thyTest" pongResponse)
