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
open Emulsion.Xmpp.SharpXmppHelper.Attributes
open Emulsion.Xmpp.SharpXmppHelper.Elements

let private createPresenceFor (roomJid: JID) nickname =
    let presence = XMPPPresence()
    let participantJid = JID(roomJid.FullJid)
    participantJid.Resource <- nickname
    presence.SetAttributeValue(From, participantJid.FullJid)
    presence

let private createSelfPresence roomJid nickname =
    let presence = createPresenceFor roomJid nickname
    let x = XElement X
    let status = XElement Status
    status.SetAttributeValue(Code, "110")
    x.Add status
    presence.Add x
    presence

let private createErrorPresence roomJid nickname errorXml =
    let presence = createPresenceFor roomJid nickname
    presence.SetAttributeValue(Type, "error")
    let error = XElement Error
    let errorChild = XElement.Parse errorXml
    error.Add errorChild
    presence.Add error
    presence

let private createLeavePresence roomJid nickname =
    let presence = createSelfPresence roomJid nickname
    presence.SetAttributeValue(Type, "unavailable")
    presence

let private sendPresence presence handlers =
    Seq.iter (fun h -> h presence) handlers

let private createErrorMessage (message: XMPPMessage) errorXml =
    // An error message is an exact copy of the original with the "error" element added:
    let errorMessage = XMPPMessage()
    message.Attributes() |> Seq.iter (fun a -> errorMessage.SetAttributeValue(a.Name, a.Value))
    message.Elements() |> Seq.iter (fun e -> errorMessage.Add e)

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
let ``connect function returns a lifetime terminated whenever the ConnectionFailed callback is triggered``()
    : unit =
        let mutable callback = ignore
        let client = XmppClientFactory.create(addConnectionFailedHandler = fun _ h -> callback <- h)
        let lt = Async.RunSynchronously <| XmppClient.connect client
        Assert.True lt.IsAlive
        callback(ConnFailedArgs())
        Assert.False lt.IsAlive

[<Fact>]
let ``enter function calls JoinMultiUserChat``(): unit =
    let mutable called = false
    let mutable presenceHandlers = ResizeArray()
    let client =
        XmppClientFactory.create(
            addPresenceHandler = (fun _ h -> presenceHandlers.Add h),
            joinMultiUserChat = fun roomJid nickname ->
                called <- true
                Seq.iter (fun h -> h (createSelfPresence roomJid nickname)) presenceHandlers
        )
    let roomInfo = { RoomJid = JID("room@conference.example.org"); Nickname = "testuser" }
    Lifetime.Using(fun lt ->
        Async.RunSynchronously <| XmppClient.enterRoom client lt roomInfo |> ignore
        Assert.True called
    )

[<Fact>]
let ``enter throws an exception in case of an error presence``(): unit =
    let mutable presenceHandlers = ResizeArray()
    let client =
        XmppClientFactory.create(
            addPresenceHandler = (fun _ h -> presenceHandlers.Add h),
            joinMultiUserChat = fun roomJid nickname ->
                sendPresence (createErrorPresence roomJid nickname "<test />") presenceHandlers
        )
    let roomInfo = { RoomJid = JID("room@conference.example.org"); Nickname = "testuser" }
    Lifetime.Using(fun lt ->
        let ae = Assert.Throws<AggregateException>(fun () ->
            Async.RunSynchronously <| XmppClient.enterRoom client lt roomInfo |> ignore
        )
        let ex = Seq.exactlyOne ae.InnerExceptions
        Assert.Contains("<test />", ex.Message)
    )

[<Fact>]
let ``Lifetime returned from enter terminates by a room leave presence``(): unit =
    let mutable presenceHandlers = ResizeArray()
    let client =
        XmppClientFactory.create(
            addPresenceHandler = (fun _ h -> presenceHandlers.Add h),
            joinMultiUserChat = fun roomJid nickname ->
                sendPresence (createSelfPresence roomJid nickname) presenceHandlers
        )
    let roomInfo = { RoomJid = JID("room@conference.example.org"); Nickname = "testuser" }
    Lifetime.Using(fun lt ->
        let roomLt = Async.RunSynchronously <| XmppClient.enterRoom client lt roomInfo
        Assert.True roomLt.IsAlive
        sendPresence (createLeavePresence roomInfo.RoomJid roomInfo.Nickname) presenceHandlers
        Assert.False roomLt.IsAlive
    )

[<Fact>]
let ``Lifetime returned from enter terminates by an external lifetime termination``(): unit =
    let mutable presenceHandlers = ResizeArray()
    let client =
        XmppClientFactory.create(
            addPresenceHandler = (fun _ h -> presenceHandlers.Add h),
            joinMultiUserChat = fun roomJid nickname ->
                sendPresence (createSelfPresence roomJid nickname) presenceHandlers
        )
    let roomInfo = { RoomJid = JID("room@conference.example.org"); Nickname = "testuser" }
    use ld = Lifetime.Define()
    let lt = ld.Lifetime
    let roomLt = Async.RunSynchronously <| XmppClient.enterRoom client lt roomInfo
    Assert.True roomLt.IsAlive
    ld.Terminate()
    Assert.False roomLt.IsAlive

[<Fact>]
let ``sendRoomMessage calls Send method on the client``(): unit =
    let mutable message = Unchecked.defaultof<XMPPMessage>
    let client = XmppClientFactory.create(send = fun m -> message <- m)
    let messageInfo = { RecipientJid = JID("room@conference.example.org"); Text = "foo bar" }
    Lifetime.Using(fun lt ->
        Async.RunSynchronously <| XmppClient.sendRoomMessage client lt messageInfo |> ignore
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
            send = fun m -> message <- m
        )
    let messageInfo = { RecipientJid = JID("room@conference.example.org"); Text = "foo bar" }
    Lifetime.Using(fun lt ->
        let deliveryInfo = Async.RunSynchronously <| XmppClient.sendRoomMessage client lt messageInfo
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
        let deliveryInfo = Async.RunSynchronously <| XmppClient.sendRoomMessage client lt messageInfo
        let deliveryTask = Async.StartAsTask deliveryInfo.Delivery
        Assert.False deliveryTask.IsCompleted

        let otherMessage = SharpXmppHelper.message (Some "xxx") "nickname@example.org" "foo bar"
        messageHandler otherMessage
        Assert.False deliveryTask.IsCompleted
    )

[<Fact>]
let ``sendRoomMessage's result gets resolved with an error if an error response is received``(): unit =
    let mutable messageHandler = ignore
    let client =
        XmppClientFactory.create(
            addMessageHandler = (fun _ h -> messageHandler <- h),
            send = fun m -> messageHandler(createErrorMessage m "<forbidden />")
        )
    let messageInfo = { RecipientJid = JID("room@conference.example.org"); Text = "foo bar" }
    Lifetime.Using(fun lt ->
        let deliveryInfo = Async.RunSynchronously <| XmppClient.sendRoomMessage client lt messageInfo
        let ae = Assert.Throws<AggregateException>(fun () -> Async.RunSynchronously deliveryInfo.Delivery)
        let ex = Seq.exactlyOne ae.InnerExceptions
        Assert.Contains("<forbidden />", ex.Message)
    )

[<Fact>]
let ``sendRoomMessage's result gets terminated after parent lifetime termination``(): unit =
    let client = XmppClientFactory.create()
    let messageInfo = { RecipientJid = JID("room@conference.example.org"); Text = "foo bar" }
    use ld = Lifetime.Define()
    let lt = ld.Lifetime
    let deliveryInfo = Async.RunSynchronously <| XmppClient.sendRoomMessage client lt messageInfo
    let deliveryTask = Async.StartAsTask deliveryInfo.Delivery
    Assert.False deliveryTask.IsCompleted
    ld.Terminate()
    Assert.Throws<TaskCanceledException>(fun () -> deliveryTask.GetAwaiter().GetResult()) |> ignore

[<Fact>]
let ``awaitMessageDelivery just returns an async from the delivery info``(): unit =
    let async = async { return () }
    let deliveryInfo = { MessageId = ""; Delivery = async }
    let result = XmppClient.awaitMessageDelivery deliveryInfo
    Assert.True(Object.ReferenceEquals(async, result))
