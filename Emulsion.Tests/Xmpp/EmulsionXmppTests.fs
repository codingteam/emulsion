module Emulsion.Tests.Xmpp.EmulsionXmppTests

open System
open System.Threading.Tasks

open JetBrains.Lifetimes
open SharpXMPP
open Xunit
open Xunit.Abstractions

open Emulsion.Settings
open Emulsion
open Emulsion.Tests.TestUtils
open Emulsion.Tests.Xmpp
open Emulsion.Xmpp
open Emulsion.Xmpp.SharpXmppHelper.Elements

let private settings = {
    Login = "user@example.org"
    Password = "password"
    Room = "room@conference.example.org"
    RoomPassword = None
    Nickname = "nickname"
    MessageTimeout = TimeSpan.FromSeconds 30.0
    PingInterval = None
    PingTimeout = defaultPingTimeout
}

let private runClientSynchronously settings logger client onMessage =
    Async.RunSynchronously <| async {
        let! runLoop = EmulsionXmpp.run settings logger client onMessage
        return! runLoop
    }

type RunTests(outputHelper: ITestOutputHelper) =
    let logger = Logging.xunitLogger outputHelper

    [<Fact>]
    member _.``EmulsionXmpp connects the server``(): unit =
        let mutable connectionFailedHandler = ignore
        let disconnect() = connectionFailedHandler(ConnFailedArgs())
        let mutable connectCalled = false
        let client =
            XmppClientFactory.create(
                addConnectionFailedHandler = (fun _ h -> connectionFailedHandler <- h),
                connect = (fun () -> async {
                    connectCalled <- true
                    disconnect()
                })
            )
        Assert.ThrowsAny<Exception>(fun() -> runClientSynchronously settings logger client ignore)
        |> ignore
        Assert.True connectCalled

    [<Fact>]
    member _.``EmulsionXmpp connects the room``(): unit =
        let mutable connectionFailedHandler = ignore
        let disconnect() = connectionFailedHandler(ConnFailedArgs())
        let mutable joinRoomArgs = Unchecked.defaultof<_>
        let client =
            XmppClientFactory.create(
                addConnectionFailedHandler = (fun _ h -> connectionFailedHandler <- h),
                joinMultiUserChat = (fun roomJid nickname _ ->
                    joinRoomArgs <- (roomJid.FullJid, nickname)
                    disconnect()
                )
            )
        Assert.ThrowsAny<Exception>(fun() -> runClientSynchronously settings logger client ignore)
        |> ignore
        Assert.Equal((settings.Room, settings.Nickname), joinRoomArgs)

type ReceiveMessageTests(outputHelper: ITestOutputHelper) =
    let logger = Logging.xunitLogger outputHelper

    let runReceiveMessageTest message =
        let mutable connectionFailedHandler = ignore
        let receiveHandlers = ResizeArray()

        let sendMessage msg = receiveHandlers |> Seq.iter (fun h -> h msg)
        let disconnect() = connectionFailedHandler(ConnFailedArgs())

        let mutable messageReceived = None
        let onMessageReceived = fun m -> messageReceived <- Some m

        let client =
            XmppClientFactory.create(
                addConnectionFailedHandler = (fun _ h -> connectionFailedHandler <- h),
                addMessageHandler = (fun _ h -> receiveHandlers.Add h),
                joinMultiUserChat = fun _ _ _ ->
                    sendMessage message
                    disconnect()
            )
        Assert.ThrowsAny<Exception>(fun() -> runClientSynchronously settings logger client onMessageReceived) |> ignore

        messageReceived

    [<Fact>]
    member _.``Ordinary message gets received by the client``(): unit =
        let incomingMessage = XmppMessageFactory.create("room@conference.example.org/sender",
                                                        "test",
                                                        messageType = "groupchat")
        let receivedMessage = runReceiveMessageTest incomingMessage
        Assert.Equal(Some <| XmppMessage (Authored { author = "sender"; text = "test" }), receivedMessage)

    [<Fact>]
    member _.``Own message gets skipped by the client``(): unit =
        let ownMessage = XmppMessageFactory.create("room@conference.example.org/nickname",
                                                   "test",
                                                   messageType = "groupchat")
        let receivedMessage = runReceiveMessageTest ownMessage
        Assert.Equal(None, receivedMessage)

    [<Fact>]
    member _.``Historical message gets skipped by the client``(): unit =
        let historicalMessage = XmppMessageFactory.create("room@conference.example.org/sender",
                                                          "test",
                                                          messageType = "groupchat",
                                                          delayDate = "2019-01-01")
        let receivedMessage = runReceiveMessageTest historicalMessage
        Assert.Equal(None, receivedMessage)

    [<Fact>]
    member _.``Empty message gets skipped by the client``(): unit =
        let emptyMessage = XmppMessageFactory.create("room@conference.example.org/sender",
                                                     "",
                                                     messageType = "groupchat")
        let receivedMessage = runReceiveMessageTest emptyMessage
        Assert.Equal(None, receivedMessage)

type SendTests(outputHelper: ITestOutputHelper) =
    let logger = Logging.xunitLogger outputHelper

    [<Fact>]
    member _.``send function calls the Send method on the client``(): unit =
        use ld = Lifetime.Define()
        let lt = ld.Lifetime
        let mutable sentMessage = Unchecked.defaultof<_>
        let client = XmppClientFactory.create(send = fun m ->
            sentMessage <- m
            ld.Terminate()
        )

        let outgoingMessage = Authored { author = "author"; text = "text" }
        Assert.Throws<TaskCanceledException>(fun () ->
            Async.RunSynchronously <| EmulsionXmpp.send logger client lt settings outgoingMessage
        ) |> ignore

        let text = sentMessage.Element(Body).Value
        Assert.Equal("<author> text", text)

    [<Fact>]
    member _.``send function awaits the message delivery``(): Task =
        upcast (async {
            use ld = Lifetime.Define()
            let lt = ld.Lifetime
            let messageId = lt.CreateTaskCompletionSource()
            let messageHandlers = ResizeArray()
            let onMessage msg = messageHandlers |> Seq.iter (fun h -> h msg)

            let client =
                XmppClientFactory.create(
                    addMessageHandler = (fun _ h -> messageHandlers.Add h),
                    send = fun m -> messageId.SetResult(Option.get <| SharpXmppHelper.getMessageId m)
                )
            let outgoingMessage = Authored { author = "author"; text = "text" }

            let! receival = Async.StartChild <| EmulsionXmpp.send logger client lt settings outgoingMessage
            let receivalTask = Async.StartAsTask receival
            let! messageId = Async.AwaitTask messageId.Task // the send has been completed

            // Wait for 100 ms to check that the receival is not completed yet:
            Assert.False(receivalTask.Wait(TimeSpan.FromMilliseconds 100.0))

            let deliveryMessage = SharpXmppHelper.message messageId "" ""
            onMessage deliveryMessage
            do! receival
        } |> Async.StartAsTask)
