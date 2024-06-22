// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

module Emulsion.Tests.Xmpp.XmppClientTests

open System
open System.Threading.Tasks
open System.Xml.Linq

open JetBrains.Lifetimes
open SharpXMPP
open SharpXMPP.XMPP
open SharpXMPP.XMPP.Client.Elements
open Xunit

open Emulsion.Xmpp
open Emulsion.Xmpp.SharpXmppHelper.Elements

let private createErrorMessage (message: XElement) errorXml =
    // An error message is an exact copy of the original with the "error" element added:
    let errorMessage = XMPPMessage()
    message.Attributes() |> Seq.iter (fun a -> errorMessage.SetAttributeValue(a.Name, a.Value))
    message.Elements() |> Seq.iter errorMessage.Add

    let error = XElement Error
    let errorChild = XElement.Parse errorXml
    error.Add errorChild
    errorMessage.Add error
    errorMessage

[<Fact>]
let ``connect function calls the Connect method of the client passed``(): unit =
    let mutable connectCalled = false
    let client = XmppClientFactory.create(fun () -> async { connectCalled <- true })
    Async.RunSynchronously <| XmppClient.connect client |> ignore
    Assert.True connectCalled

[<Fact>]
let ``connect function returns a lifetime terminated whenever the ConnectionFailed callback is triggered``(): unit =
    let mutable callback = ignore
    let client = XmppClientFactory.create(addConnectionFailedHandler = fun _ h -> callback <- h)
    let lt = Async.RunSynchronously <| XmppClient.connect client
    Assert.True lt.IsAlive
    callback(ConnFailedArgs())
    Assert.False lt.IsAlive

let private sendRoomMessage client lt messageInfo =
    XmppClient.sendRoomMessage client lt Emulsion.Settings.defaultMessageTimeout messageInfo

[<Fact>]
let ``sendRoomMessage calls Send method on the client``(): unit =
    let mutable message = Unchecked.defaultof<XMPPMessage>
    let client = XmppClientFactory.create(send = fun m -> message <- m :?> XMPPMessage)
    let messageInfo = { RecipientJid = JID("room@conference.example.org"); Text = "foo bar" }
    Lifetime.Using(fun lt ->
        Async.RunSynchronously <| sendRoomMessage client lt messageInfo |> ignore
        Assert.Equal(messageInfo.RecipientJid.FullJid, message.To.FullJid)
        Assert.Equal(messageInfo.Text, message.Text)
    )

[<Fact>]
let ``sendRoomMessage's result gets resolved after the message receival``(): unit =
    let mutable messageHandler = ignore
    let mutable message = Unchecked.defaultof<XMPPMessage>
    let client =
        XmppClientFactory.create(
            addMessageHandler = (fun _ h -> messageHandler <- h),
            send = fun m -> message <- m :?> XMPPMessage
        )
    let messageInfo = { RecipientJid = JID("room@conference.example.org"); Text = "foo bar" }
    Lifetime.Using(fun lt ->
        let deliveryInfo = Async.RunSynchronously <| sendRoomMessage client lt messageInfo
        Assert.Equal(message.ID, deliveryInfo.MessageId)
        let deliveryTask = Async.StartAsTask deliveryInfo.Delivery
        Assert.False deliveryTask.IsCompleted
        messageHandler message
        deliveryTask.Wait()
    )

[<Fact>]
let ``sendRoomMessage's result doesn't get resolved after receiving other message``(): unit =
    let mutable messageHandler = ignore
    let client = XmppClientFactory.create(addMessageHandler = fun _ h -> messageHandler <- h)
    let messageInfo = { RecipientJid = JID("room@conference.example.org"); Text = "foo bar" }
    Lifetime.Using(fun lt ->
        let deliveryInfo = Async.RunSynchronously <| sendRoomMessage client lt messageInfo
        let deliveryTask = Async.StartAsTask deliveryInfo.Delivery
        Assert.False deliveryTask.IsCompleted

        let otherMessage = SharpXmppHelper.message "xxx" "nickname@example.org" "foo bar"
        messageHandler otherMessage
        Assert.False deliveryTask.IsCompleted
    )

[<Fact>]
let ``sendRoomMessage's result gets resolved with an error if an error response is received``(): unit =
    let mutable messageHandler = ignore
    let client =
        XmppClientFactory.create(
            addMessageHandler = (fun _ h -> messageHandler <- h),
            send = fun m -> messageHandler(createErrorMessage m "<forbidden xmlns=\"urn:ietf:params:xml:ns:xmpp-stanzas\" />")
        )
    let messageInfo = { RecipientJid = JID("room@conference.example.org"); Text = "foo bar" }
    Lifetime.Using(fun lt ->
        let deliveryInfo = Async.RunSynchronously <| sendRoomMessage client lt messageInfo
        let ae = Assert.Throws<AggregateException>(fun () -> Async.RunSynchronously deliveryInfo.Delivery)
        let ex = Seq.exactlyOne ae.InnerExceptions
        Assert.Contains("<forbidden xmlns=\"urn:ietf:params:xml:ns:xmpp-stanzas\" />", ex.Message)
    )

[<Fact>]
let ``sendRoomMessage's result gets terminated after parent lifetime termination``(): unit =
    let client = XmppClientFactory.create()
    let messageInfo = { RecipientJid = JID("room@conference.example.org"); Text = "foo bar" }
    use ld = Lifetime.Define()
    let lt = ld.Lifetime
    let deliveryInfo = Async.RunSynchronously <| sendRoomMessage client lt messageInfo
    let deliveryTask = Async.StartAsTask deliveryInfo.Delivery
    Assert.False deliveryTask.IsCompleted
    ld.Terminate()
    Assert.Throws<TaskCanceledException>(deliveryTask.GetAwaiter().GetResult) |> ignore

[<Fact>]
let ``awaitMessageDelivery just returns an async from the delivery info``(): unit =
    let async = async { return () }
    let deliveryInfo = { MessageId = ""; Delivery = async }
    let result = XmppClient.awaitMessageDelivery deliveryInfo
    Assert.True(Object.ReferenceEquals(async, result))

[<Fact>]
let ``awaitMessageDelivery should throw an error on timeout``(): unit =
    let client = XmppClientFactory.create()
    let messageInfo = { RecipientJid = JID("room@conference.example.org"); Text = "foo bar" }
    use ld = Lifetime.Define()
    let lt = ld.Lifetime
    let deliveryInfo = Async.RunSynchronously <| XmppClient.sendRoomMessage client lt TimeSpan.Zero messageInfo
    let deliveryTask = XmppClient.awaitMessageDelivery deliveryInfo
    Assert.Throws<TimeoutException>(fun () -> Async.RunSynchronously deliveryTask) |> ignore
