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
    let expected = Some(XmppMessage("author", text))
    Assert.Equal(expected, message)

[<Fact>]
let ``Message without author parses to None``() =
    let element = XMPPMessage(Text = "xxx")
    let message = SharpXmppHelper.parseMessage element
    Assert.Equal(None, message)

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
