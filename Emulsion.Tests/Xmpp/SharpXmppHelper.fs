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
    element.Attribute(XName.Get "from").Value <- author
    let message = SharpXmppHelper.parseMessage element
    Assert.Equal(XmppMessage(author, text), message)
