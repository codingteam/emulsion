/// Main business logic for an XMPP part of the Emulsion application.
module Emulsion.Xmpp.EmulsionXmpp

open System

open JetBrains.Lifetimes
open Serilog
open SharpXMPP.XMPP

open Emulsion
open Emulsion.Settings
open Emulsion.MessageSystem
open Emulsion.Xmpp.XmppClient

let private shouldProcessMessage (settings: XmppSettings) message =
    let isGroup = SharpXmppHelper.isGroupChatMessage message
    let shouldSkip = lazy (
        SharpXmppHelper.isOwnMessage (settings.Nickname) message
        || SharpXmppHelper.isHistoricalMessage message
        || SharpXmppHelper.isEmptyMessage message
    )
    isGroup && not shouldSkip.Value

let private addMessageHandler (client: IXmppClient) lt settings receiver =
    client.AddMessageHandler lt (fun xmppMessage ->
        if shouldProcessMessage settings xmppMessage then
            let message = SharpXmppHelper.parseMessage xmppMessage
            receiver(XmppMessage message)
    )

let initializeLogging (logger: ILogger) (client: IXmppClient): IXmppClient =
    let lt = Lifetime.Eternal
    client.AddConnectionFailedHandler lt (fun e -> logger.Error(e.Exception, "Connection failed: {Message}", e.Message))
    client.AddSignedInHandler lt (fun _ -> logger.Information("Signed in to the server"))
    client.AddElementHandler lt (fun e ->
        let direction = if e.IsInput then "incoming" else "outgoing"
        logger.Verbose("XMPP stanza ({Direction}): {Stanza}", direction, e.Stanza)
    )
    client

let private withTimeout title (logger: ILogger) workflow (timeout: TimeSpan) = async {
    logger.Information("Starting \"{Title}\" with timeout {Timeout}.", title, timeout)
    let! child = Async.StartChild(workflow, int timeout.TotalMilliseconds)

    let! childWaiter = Async.StartChild(async {
        let! result = child
        return Some(ValueSome result)
    })

    let waitTime = timeout * 1.5
    let timeoutWaiter = async {
        do! Async.Sleep waitTime
        return Some ValueNone
    }

    let! completedInTime = Async.Choice [| childWaiter; timeoutWaiter |]
    match completedInTime with
    | Some(ValueSome r) -> return r
    | _ ->
        logger.Information(
            "Task {Title} neither complete nor cancelled in {Timeout}. Entering extended wait mode.",
            title,
            waitTime
        )
        let! completedInTime = Async.Choice [| childWaiter; timeoutWaiter |]
        match completedInTime with
        | Some(ValueSome r) -> return r
        | _ ->
            logger.Warning(
                "Task {Title} neither complete nor cancelled in another {Timeout}. Trying to cancel forcibly by terminating the client.",
                title,
                waitTime
            )
            return raise <| OperationCanceledException($"Operation \"%s{title}\" forcibly cancelled")
}

/// Outer async will establish a connection and enter the room, inner async will await for the room session
/// termination.
let run (settings: XmppSettings)
        (logger: ILogger)
        (client: IXmppClient)
        (messageReceiver: IncomingMessageReceiver): Async<Async<unit>> = async {
    let! sessionLifetime = withTimeout "server connection" logger (connect client) settings.ConnectionTimeout
    sessionLifetime.ThrowIfNotAlive()
    logger.Information "Connection succeeded"

    logger.Information "Initializing client handler"
    addMessageHandler client sessionLifetime settings messageReceiver
    logger.Information "Client handler initialized"

    let roomInfo = {
        RoomJid = JID(settings.Room)
        Nickname = settings.Nickname
        Password = settings.RoomPassword
        Ping = {| Interval = settings.PingInterval
                  Timeout = settings.PingTimeout |}
    }
    logger.Information("Entering the room {RoomInfo}", roomInfo)
    let! roomLifetime = enterRoom logger client sessionLifetime roomInfo
    logger.Information "Entered the room"

    return async {
        logger.Information "Ready, waiting for room lifetime termination"
        do! Lifetimes.awaitTermination roomLifetime
        logger.Information "Room lifetime has been terminated"
    }
}

let send (logger: ILogger)
         (client: IXmppClient)
         (lifetime: Lifetime)
         (settings: XmppSettings)
         (message: Message): Async<unit> = async {
    let text =
        match message with
        | Authored msg -> sprintf "<%s> %s" msg.author msg.text
        | Event msg -> sprintf "%s" msg.text
    let message = { RecipientJid = JID(settings.Room); Text = text }
    let! deliveryInfo = sendRoomMessage client lifetime settings.MessageTimeout message
    logger.Information("Message {MessageId} has been sent; awaiting delivery", deliveryInfo.MessageId)
    do! awaitMessageDelivery deliveryInfo
}
