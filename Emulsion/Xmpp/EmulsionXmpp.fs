/// Main business logic for an XMPP part of the Emulsion application.
module Emulsion.Xmpp.EmulsionXmpp

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

/// Outer async will establish a connection and enter the room, inner async will await for the room session
/// termination.
let run (settings: XmppSettings)
        (logger: ILogger)
        (client: IXmppClient)
        (messageReceiver: IncomingMessageReceiver): Async<Async<unit>> = async {
    logger.Information "Connecting to the server"
    let! sessionLifetime = connect client
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
