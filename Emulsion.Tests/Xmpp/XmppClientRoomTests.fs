namespace Emulsion.Tests.Xmpp

open System
open System.Threading.Tasks
open System.Xml.Linq

open FSharp.Control.Tasks
open JetBrains.Lifetimes
open SharpXMPP.XMPP
open SharpXMPP.XMPP.Client.Elements
open Xunit
open Xunit.Abstractions

open Emulsion
open Emulsion.Xmpp
open Emulsion.Xmpp.SharpXmppHelper.Attributes
open Emulsion.Xmpp.SharpXmppHelper.Elements
open Emulsion.Tests.TestUtils.Logging

type XmppClientRoomTests(output: ITestOutputHelper) =
    let logger = xunitLogger output

    let testRoomInfo = {
        RoomJid = JID("room@conference.example.org")
        Nickname = "testuser"
        Password = None
        Ping = {| Interval = None
                  Timeout = Settings.defaultPingTimeout |}
    }

    let roomInfoWithPing =
        { testRoomInfo with
             Ping = {| testRoomInfo.Ping with Interval = Some(TimeSpan.FromHours 1.0) |}
        }

    let createPresenceFor (roomJid: JID) nickname =
        let presence = XMPPPresence()
        let participantJid = JID(roomJid.FullJid)
        participantJid.Resource <- nickname
        presence.SetAttributeValue(From, participantJid.FullJid)
        presence

    let createSelfPresence roomJid nickname (statusCode: int) =
        let presence = createPresenceFor roomJid nickname
        let x = XElement X
        let status = XElement Status
        status.SetAttributeValue(Code, statusCode)
        x.Add status
        presence.Add x
        presence

    let createErrorPresence roomJid nickname errorXml =
        let presence = createPresenceFor roomJid nickname
        presence.SetAttributeValue(Type, "error")
        let error = XElement Error
        let errorChild = XElement.Parse errorXml
        error.Add errorChild
        presence.Add error
        presence

    let createLeavePresence roomJid nickname =
        let presence = createSelfPresence roomJid nickname 307
        presence.SetAttributeValue(Type, "unavailable")
        presence

    let sendPresence presence handlers =
        Seq.iter (fun h -> h presence) handlers

    let sendPong roomInfo id handler =
        let pong = XMPPIq(XMPPIq.IqTypes.result, id)
        pong.SetAttributeValue(From, sprintf "%s/%s" roomInfo.RoomJid.BareJid roomInfo.Nickname)
        handler pong

    let createPresenceHandlingClient() =
        let mutable presenceHandlers = ResizeArray()
        XmppClientFactory.create(
            addPresenceHandler = (fun _ -> presenceHandlers.Add),
            joinMultiUserChat = fun roomJid nickname _ ->
                sendPresence (createSelfPresence roomJid nickname 110) presenceHandlers
        ), presenceHandlers

    let pingWaitTime = TimeSpan.FromMilliseconds 100.0
    let assertNoPingSent(iqMessages: ResizeArray<XMPPIq>) = async {
        do! Async.Sleep(int pingWaitTime.TotalMilliseconds)
        lock iqMessages (fun() ->
            Assert.Empty iqMessages
        )
    }

    let assertPingSent(iqMessages: ResizeArray<XMPPIq>) = async {
        do! Async.Sleep(int pingWaitTime.TotalMilliseconds)
        lock iqMessages (fun() ->
            let iq = Seq.exactlyOne iqMessages
            let ping = iq.Element Ping
            Assert.NotNull ping
        )
    }

    [<Fact>]
    member _.``enterRoom function calls JoinMultiUserChat``(): unit =
        let mutable called = false
        let presenceHandlers = ResizeArray()
        let client =
            XmppClientFactory.create(
                addPresenceHandler = (fun _ -> presenceHandlers.Add),
                joinMultiUserChat = fun roomJid nickname _ ->
                    called <- true
                    Seq.iter (fun h -> h (createSelfPresence roomJid nickname 110)) presenceHandlers
            )
        Lifetime.Using(fun lt ->
            Async.RunSynchronously <| XmppClient.enterRoom logger client lt testRoomInfo |> ignore
            Assert.True called
        )

    [<Fact>]
    member _.``enterRoom throws an exception in case of an error presence``(): unit =
        let mutable presenceHandlers = ResizeArray()
        let client =
            XmppClientFactory.create(
                addPresenceHandler = (fun _ -> presenceHandlers.Add),
                joinMultiUserChat = fun roomJid nickname _ ->
                    sendPresence (createErrorPresence roomJid nickname "<jid-malformed xmlns=\"urn:ietf:params:xml:ns:xmpp-stanzas\" />") presenceHandlers
            )
        Lifetime.Using(fun lt ->
            let ae = Assert.Throws<AggregateException>(fun () ->
                Async.RunSynchronously <| XmppClient.enterRoom logger client lt testRoomInfo |> ignore
            )
            let ex = Seq.exactlyOne ae.InnerExceptions
            Assert.Contains("<jid-malformed xmlns=\"urn:ietf:params:xml:ns:xmpp-stanzas\" />", ex.Message)
        )

    [<Fact>]
    member _.``Lifetime returned from enterRoom terminates by a room leave presence``(): unit =
        let (client, presenceHandlers) = createPresenceHandlingClient()
        Lifetime.Using(fun lt ->
            let roomLt = Async.RunSynchronously <| XmppClient.enterRoom logger client lt testRoomInfo
            Assert.True roomLt.IsAlive
            sendPresence (createLeavePresence testRoomInfo.RoomJid testRoomInfo.Nickname) presenceHandlers
            Assert.False roomLt.IsAlive
        )

    [<Fact>]
    member _.``Lifetime returned from enterRoom terminates by an external lifetime termination``(): unit =
        let (client, _) = createPresenceHandlingClient()
        use ld = Lifetime.Define()
        let lt = ld.Lifetime
        let roomLt = Async.RunSynchronously <| XmppClient.enterRoom logger client lt testRoomInfo
        Assert.True roomLt.IsAlive
        ld.Terminate()
        Assert.False roomLt.IsAlive

    [<Fact>]
    member _.``Client sends a ping after room connection``(): Task =
        let presenceHandlers = ResizeArray()
        let iqMessages = ResizeArray()
        let client =
            XmppClientFactory.create(
                addPresenceHandler = (fun _ -> presenceHandlers.Add),
                joinMultiUserChat = (fun roomJid nickname _ ->
                    sendPresence (createSelfPresence roomJid nickname 110) presenceHandlers),
                sendIqQuery = fun _ iq _ -> lock iqMessages (fun() -> iqMessages.Add iq)
            )

        upcast Lifetime.UsingAsync(fun lt ->
            async {
                let! _ = XmppClient.enterRoom logger client lt roomInfoWithPing
                do! assertPingSent iqMessages
            } |> Async.StartAsTask
        )

    [<Fact>]
    member _.``Client doesn't send ping before finishing joining the room``(): Task =
        let presenceHandlers = ResizeArray()
        let iqMessages = ResizeArray()
        let mutable join = ref Unchecked.defaultof<_>
        let client =
            XmppClientFactory.create(
                addPresenceHandler = (fun _ h -> lock presenceHandlers (fun() -> presenceHandlers.Add h)),
                joinMultiUserChat = (fun roomJid nickname _ ->
                    lock join (fun() ->
                        join := fun () -> lock presenceHandlers (fun() ->
                            sendPresence (createSelfPresence roomJid nickname 110) presenceHandlers)
                        )
                    ),
                sendIqQuery = fun _ iq _ -> lock iqMessages (fun() -> iqMessages.Add iq)
            )
        upcast Lifetime.UsingAsync(fun lt ->
            async {
                let! connection = Async.StartChild <| XmppClient.enterRoom logger client lt roomInfoWithPing
                do! assertNoPingSent iqMessages
                lock join !join // ha-ha
                let! _ = connection
                do! assertPingSent iqMessages
            } |> Async.StartAsTask
        )

    [<Fact>]
    member _.``Client disconnects on failing a ping request``(): Task =
        let timeout = TimeSpan.FromMilliseconds 500.0
        let roomInfo = { roomInfoWithPing with Ping = {| roomInfoWithPing.Ping with Timeout = timeout |} }

        let (client, _) = createPresenceHandlingClient()
        upcast Lifetime.UsingAsync(fun lt ->
            async {
                let! lt = XmppClient.enterRoom logger client lt roomInfo
                Assert.True lt.IsAlive
                do! Async.Sleep(int (timeout * 2.0).TotalMilliseconds) // wait for two timeouts for test stability
                Assert.False lt.IsAlive
            } |> Async.StartAsTask
        )

    [<Fact>]
    member _.``Client doesn't disconnect on successful ping request``(): Task =
        let timeout = TimeSpan.FromMilliseconds 500.0
        let roomInfo = { roomInfoWithPing with Ping = {| roomInfoWithPing.Ping with Timeout = timeout |} }

        let presenceHandlers = ResizeArray()
        let client =
            XmppClientFactory.create(
                addPresenceHandler = (fun _ -> presenceHandlers.Add),
                joinMultiUserChat = (fun roomJid nickname _ ->
                    sendPresence (createSelfPresence roomJid nickname 110) presenceHandlers),
                sendIqQuery = fun _ iq handler ->
                    let pingId = iq.ID
                    sendPong roomInfo pingId handler
            )

        upcast Lifetime.UsingAsync(fun lt ->
            async {
                let! lt = XmppClient.enterRoom logger client lt roomInfo
                Assert.True lt.IsAlive
                do! Async.Sleep(int (timeout * 2.0).TotalMilliseconds)
                Assert.True lt.IsAlive
            } |> Async.StartAsTask
        )

    [<Fact>]
    member _.``Exception should be thrown if pingInterval < pingTimeout``(): Task =
        let interval = TimeSpan.FromMilliseconds 300.0
        let timeout = TimeSpan.FromMilliseconds 500.0
        let roomInfo =
            { roomInfoWithPing with
                Ping = {| Interval = Some interval
                          Timeout = timeout |}
            }

        let (client, _) = createPresenceHandlingClient()
        upcast task {
            let! ex = Assert.ThrowsAnyAsync(fun() ->
                upcast Lifetime.UsingAsync(fun lt ->
                    async {
                        let! _ = XmppClient.enterRoom logger client lt roomInfo
                        ()
                    } |> Async.StartAsTask
                )
            )

            let expectedMessage = sprintf "Ping interval of %A should be greater than ping timeout of %A" interval timeout
            Assert.Equal(expectedMessage, ex.Message)
        }
