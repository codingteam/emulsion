namespace Emulsion.Tests.Xmpp

open System
open System.Threading.Tasks
open System.Xml.Linq

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

    let createPresenceHandlingClient() =
        let mutable presenceHandlers = ResizeArray()
        XmppClientFactory.create(
            addPresenceHandler = (fun _ -> presenceHandlers.Add),
            joinMultiUserChat = fun roomJid nickname _ ->
                sendPresence (createSelfPresence roomJid nickname 110) presenceHandlers
        ), presenceHandlers

    [<Fact>]
    member _.``enterRoom function calls JoinMultiUserChat``(): unit =
        let mutable called = false
        let mutable presenceHandlers = ResizeArray()
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
    member _.``client sends a ping after room connection``(): Task =
        let (client, _) = createPresenceHandlingClient()
        upcast Lifetime.UsingAsync(fun lt ->
            async {
                let mutable pingSent = false
                let! _ = XmppClient.enterRoom logger client lt testRoomInfo
                Assert.True pingSent
            } |> Async.StartAsTask
        )

    [<Fact>]
    member _.``client doesn't send ping before finishing joining the room``(): unit =
        Assert.True false

    [<Fact>]
    member _.``client disconnects on failing a ping request``(): unit =
        Assert.True false

    [<Fact>]
    member _.``client doesn't disconnect on successful ping request``(): unit =
        Assert.True false
