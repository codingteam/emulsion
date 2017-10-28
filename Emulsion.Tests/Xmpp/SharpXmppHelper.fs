module Emulsion.Tests.Xmpp.SharpXmppHelper

open System.Xml.Linq

open SharpXMPP.XMPP.Client.Elements

open Xunit

open Emulsion
open Emulsion.Xmpp

[<Fact>]
let ``parseMessage should extract message text and author``() =
    let author = "author"
    let text = "text test"
    let element = XMPPMessage(Text = text)
    element.SetAttributeValue(XName.Get "from", author)
    let message = SharpXmppHelper.parseMessage element
    let expected = Some(XmppMessage(author, text))
    Assert.Equal(expected, message)

[<Fact>]
let ``Message without author parses to None``() =
    let element = XMPPMessage(Text = "xxx")
    let message = SharpXmppHelper.parseMessage element
    Assert.Equal(None, message)
