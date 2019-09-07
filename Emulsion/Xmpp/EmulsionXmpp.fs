/// Main business logic for an XMPP part of the Emulsion application.
/// TODO[F]: Add tests for this module.
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
    client.AddSignedInHandler lt (fun e -> logger.Information("Signed in to the server"))
    client.AddElementHandler lt (fun e ->
        let direction = if e.IsInput then "incoming" else "outgoing"
        logger.Verbose("XMPP stanza ({Direction}): {Stanza}", direction, e.Stanza)
    )
    client

let run (settings: XmppSettings)
        (logger: ILogger)
        (client: IXmppClient)
        (messageReceiver: IncomingMessageReceiver): Async<unit> = async {
    logger.Information "Connecting to the server"
    let! sessionLifetime = XmppClient.connect logger client
    logger.Information "Connection succeeded"

    logger.Information "Initializing client handler"
    addMessageHandler client sessionLifetime settings messageReceiver
    logger.Information "Client handler initialized"

    let roomInfo = { RoomJid = JID(settings.Room); Nickname = settings.Nickname }
    logger.Information("Entering the room {RoomInfo}", roomInfo)
    let! roomLifetime = XmppClient.enterRoom client sessionLifetime roomInfo
    logger.Information "Entered the room"

    logger.Information "Ready, waiting for room lifetime termination"
    do! Lifetimes.awaitTermination roomLifetime
    logger.Information "Room lifetime has been terminated"
}

let send (logger: ILogger)
         (client: IXmppClient)
         (lifetime: Lifetime)
         (settings: XmppSettings)
         (message: Message): Async<unit> = async {
    let text = sprintf "<%s> %s" message.author message.text
    let message = { RecipientJid = JID(settings.Room); Text = text }
    let! deliveryInfo = XmppClient.sendRoomMessage client lifetime message
    logger.Information("Message {MessageId} has been sent; awaiting delivery", deliveryInfo.MessageId)
    do! XmppClient.awaitMessageDelivery deliveryInfo
}
