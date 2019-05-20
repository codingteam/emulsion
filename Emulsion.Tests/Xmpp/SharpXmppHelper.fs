module Emulsion.Tests.Xmpp.SharpXmppHelper

open System.Xml.Linq

open SharpXMPP.XMPP.Client.Elements

open Xunit

open Emulsion
open Emulsion.Xmpp

let private createXmppMessage (senderJid : string) (text : string) =
    let element = XMPPMessage(Text = text)
    element.SetAttributeValue(XName.Get "from", senderJid)
    element

[<Fact>]
let ``parseMessage should extract message text and author``() =
    let text = "text test"
    let element = createXmppMessage "x@y/author" text
    let message = SharpXmppHelper.parseMessage element
    let expected = XmppMessage { author = "author"; text = text }
    Assert.Equal(expected, message)

[<Fact>]
let ``Message without author is attributed to [UNKNOWN USER]``() =
    let text = "xxx"
    let element = XMPPMessage(Text = text)
    let message = SharpXmppHelper.parseMessage element
    let expected = XmppMessage { author = "[UNKNOWN USER]"; text = text }
    Assert.Equal(expected, message)

[<Fact>]
let ``isOwnMessage detects own message by resource``() =
    let message = createXmppMessage "a@b/myNickname" "text"
    Assert.True(SharpXmppHelper.isOwnMessage "myNickname" message)

[<Fact>]
let ``isOwnMessage detects foreign message``() =
    let message = createXmppMessage "a@b/notMyNickname" "text"
    Assert.False(SharpXmppHelper.isOwnMessage "myNickname" message)

[<Fact>]
let ``isOwnMessage detects nobody's message``() =
    let message = XMPPMessage() // no "from" attribute
    Assert.False(SharpXmppHelper.isOwnMessage "myNickname" message)
