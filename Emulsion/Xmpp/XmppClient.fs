/// A general abstraction around an XMPP client and common functions.
module Emulsion.Xmpp.XmppClient

open System
open System.Threading
open System.Xml.Linq

open JetBrains.Lifetimes
open Serilog
open SharpXMPP
open SharpXMPP.XMPP
open SharpXMPP.XMPP.Client.Elements

open Emulsion
open Emulsion.Xmpp

/// Note: all the subscription methods should be free-threaded, and should expect the lifetime to be terminated in a
/// free-threaded way (i.e. lifetime termination may come from any thread in the system).
type IXmppClient =
    abstract member Connect: unit -> Async<unit>
    abstract member JoinMultiUserChat: roomJid: JID -> nickname: string -> password: string option -> unit
    /// Sends an XMPP message. Should be free-threaded.
    abstract member Send: XElement -> unit
    /// Sends an IQ query and adds a handler for the query response. Should be free-threaded.
    abstract member SendIqQuery: Lifetime -> XMPPIq -> (XMPPIq -> unit) -> unit
    abstract member AddConnectionFailedHandler: Lifetime -> (ConnFailedArgs -> unit) -> unit
    abstract member AddSignedInHandler: Lifetime -> (SignedInArgs -> unit) -> unit
    abstract member AddElementHandler: Lifetime -> (ElementArgs -> unit) -> unit
    abstract member AddPresenceHandler: Lifetime -> (XMPPPresence -> unit) -> unit
    abstract member AddMessageHandler: Lifetime -> (XMPPMessage -> unit) -> unit

/// Establish a connection to the server and log in. Returns a connection lifetime that will terminate if the connection
/// terminates.
let connect (client: IXmppClient): Async<Lifetime> = async {
    let connectionLifetime = new LifetimeDefinition()
    client.AddConnectionFailedHandler connectionLifetime.Lifetime <| fun _ ->
        connectionLifetime.Terminate()

    do! client.Connect()
    return connectionLifetime.Lifetime
}

let private botJid roomInfo =
    sprintf "%s/%s" roomInfo.RoomJid.BareJid roomInfo.Nickname

let private isSelfPresence (roomInfo: RoomInfo) (presence: XMPPPresence) =
    let presence = SharpXmppHelper.parsePresence presence
    let expectedJid = botJid roomInfo
    presence.Type = None && presence.From = expectedJid && Array.contains 110 presence.States

let private isLeavePresence (roomInfo: RoomInfo) (presence: XMPPPresence) =
    let presence = SharpXmppHelper.parsePresence presence
    let expectedJid = botJid roomInfo
    presence.From = expectedJid && presence.Type = Some "unavailable" && SharpXmppHelper.hasRemovalCode presence.States

let private extractPresenceException (roomInfo: RoomInfo) (presence: XMPPPresence) =
    let presence = SharpXmppHelper.parsePresence presence
    let expectedJid = sprintf "%s/%s" roomInfo.RoomJid.BareJid roomInfo.Nickname
    if presence.From = expectedJid then
        presence.Error
        |> Option.map (fun e -> Exception(sprintf "Error: %A" e))
    else None

let private newMessageId(): string =
    Guid.NewGuid().ToString()

let private startPingActivity (logger: ILogger)
                              (client: IXmppClient)
                              (roomLifetimeDefinition: LifetimeDefinition)
                              roomInfo =
    roomInfo.Ping.Interval |> Option.iter (fun pingInterval ->
        let pingTimeout = roomInfo.Ping.Timeout
        if pingInterval <= pingTimeout then
            failwithf "Ping interval of %A should be greater than ping timeout of %A" pingInterval pingTimeout
            // (otherwise, `use pingLifetimeDefinition` below would create lifetime conflict: it could've been
            // terminated earlier than the ping timeout ends, which will break ping logic)

        let activityLifetime = roomLifetimeDefinition.Lifetime
        let jid = JID(botJid roomInfo)
        Async.Start (async {
            while true do
                try
                    let pingId = newMessageId()
                    let pingMessage = SharpXmppHelper.ping jid pingId
                    let mutable pongReceived = false

                    use pingLifetimeDefinition = activityLifetime.CreateNested()
                    let pingLifetime =
                        pingLifetimeDefinition.Lifetime
                           .CreateTerminatedAfter(pingTimeout)
                           .OnTermination(fun () ->
                                let pongReceived = Volatile.Read &pongReceived
                                if not pongReceived then
                                    logger.Warning("Ping message not received in {Time}: terminating room {Room}",
                                                   roomInfo.Ping.Timeout,
                                                   roomInfo.RoomJid)
                                    roomLifetimeDefinition.Terminate()
                            )

                    client.SendIqQuery pingLifetime pingMessage (fun response ->
                        if SharpXmppHelper.isPong jid pingId response then
                            Volatile.Write(&pongReceived, true)
                            pingLifetimeDefinition.Terminate()
                    )

                    do! Async.Sleep(int pingInterval.TotalMilliseconds)
                with
                | ex ->
                    logger.Error(ex, "Exception in ping activity for {Room}: {Message}", roomInfo.RoomJid, ex.Message)
                    roomLifetimeDefinition.Terminate()
        }, activityLifetime.ToCancellationToken())
    )

/// Enter the room, returning the in-room lifetime. Will terminate if kicked or left the room.
let enterRoom (logger: ILogger) (client: IXmppClient) (lifetime: Lifetime) (roomInfo: RoomInfo): Async<Lifetime> =
    async {
        use connectionLifetimeDefinition = lifetime.CreateNested()
        let connectionLifetime = connectionLifetimeDefinition.Lifetime

        let roomLifetimeDefinition = lifetime.CreateNested()
        let roomLifetime = roomLifetimeDefinition.Lifetime

        let tcs = connectionLifetime.CreateTaskCompletionSource()

        // Success and error handlers:
        client.AddPresenceHandler connectionLifetime (fun presence ->
            if isSelfPresence roomInfo presence
            then tcs.SetResult()
            else
                match extractPresenceException roomInfo presence with
                | Some ex -> tcs.SetException ex
                | None -> ()
        )

        // Room leave handler:
        client.AddPresenceHandler roomLifetime (fun presence ->
            if isLeavePresence roomInfo presence
            then roomLifetimeDefinition.Terminate()
        )

        try
            // Start the join process, wait for a result:
            client.JoinMultiUserChat roomInfo.RoomJid roomInfo.Nickname roomInfo.Password
            do! Async.AwaitTask tcs.Task
            startPingActivity logger client roomLifetimeDefinition roomInfo
            return roomLifetime
        with
        | ex ->
            // In case of an error, terminate the room lifetime (but leave it intact in case of success):
            roomLifetimeDefinition.Terminate()
            return ExceptionUtils.reraise ex
    }

let private hasMessageId messageId message =
    SharpXmppHelper.getMessageId message = Some messageId

let private extractMessageException message =
    SharpXmppHelper.getMessageError message
    |> Option.map(fun e -> Exception(sprintf "Error: %A" e))

let private awaitMessageReceival (client: IXmppClient) (lifetime: Lifetime) (timeout: TimeSpan) messageId =
    // We need to perform this part synchronously to avoid the race condition between adding a message handler and
    // actually sending a message.
    let messageLifetimeDefinition = lifetime.CreateNested()
    try
        let messageLifetime = messageLifetimeDefinition.Lifetime
        let messageReceivedTask = messageLifetime.CreateTaskCompletionSource()
        client.AddMessageHandler lifetime (fun message ->
            if hasMessageId messageId message then
                match extractMessageException message with
                | Some ex -> messageReceivedTask.SetException ex
                | None -> messageReceivedTask.SetResult()
        )
        async {
            try
                let! task = Async.StartChild(Async.AwaitTask messageReceivedTask.Task, int timeout.TotalMilliseconds)
                do! task
            finally
                messageLifetimeDefinition.Dispose()
        }
    with
    | _ ->
        messageLifetimeDefinition.Dispose()
        reraise()

/// Sends the message to the room. Returns an object that allows to track the message receival.
let sendRoomMessage (client: IXmppClient)
                    (lifetime: Lifetime)
                    (timeout: TimeSpan)
                    (messageInfo: MessageInfo): Async<MessageDeliveryInfo> =
    async {
        let messageId = newMessageId()
        let message = SharpXmppHelper.message messageId messageInfo.RecipientJid.FullJid messageInfo.Text
        let! delivery = Async.StartChild <| awaitMessageReceival client lifetime timeout messageId
        client.Send message
        return {
            MessageId = messageId
            Delivery = delivery
        }
    }

/// Waits for the message to be delivered.
let awaitMessageDelivery (deliveryInfo: MessageDeliveryInfo): Async<unit> =
    deliveryInfo.Delivery
