module Emulsion.Tests.Xmpp.SharpXmppHelper

open Xunit

open Emulsion
open Emulsion.Tests.Xmpp
open Emulsion.Xmpp

[<Fact>]
let ``parseMessage should extract message text and author``() =
    let text = "text test"
    let element = XmppMessageFactory.create("x@y/author", text)
    let message = SharpXmppHelper.parseMessage element
    let expected = XmppMessage { author = "author"; text = text }
    Assert.Equal(expected, message)

[<Fact>]
let ``Message without author is attributed to [UNKNOWN USER]``() =
    let text = "xxx"
    let element = XmppMessageFactory.create(text = text)
    let message = SharpXmppHelper.parseMessage element
    let expected = XmppMessage { author = "[UNKNOWN USER]"; text = text }
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
