module Emulsion.Xmpp.XmppClient

open System
open System.Threading.Tasks

open JetBrains.Lifetimes
open Serilog
open SharpXMPP
open SharpXMPP.XMPP

open Emulsion
open Emulsion.Settings
open SharpXMPP.XMPP.Client.Elements

type IXmppClient =
    abstract member Connect: unit -> Async<unit>
    abstract member JoinMultiUserChat: roomJid: JID -> nickname: string -> unit
    abstract member Send: XMPPMessage -> unit
    abstract member AddConnectionFailedHandler: Lifetime -> (ConnFailedArgs -> unit) -> unit
    abstract member AddPresenceHandler: Lifetime -> (XMPPPresence -> unit) -> unit
    abstract member AddMessageHandler: Lifetime -> (XMPPMessage -> unit) -> unit

type SharpXmppClient(client: XmppClient) =
    interface IXmppClient with
        member ___.Connect() = async {
            let! ct = Async.CancellationToken
            return! Async.AwaitTask(client.ConnectAsync ct)
        }
        member __.JoinMultiUserChat roomJid nickname = SharpXmppHelper.joinRoom client roomJid.BareJid nickname
        member __.Send message = client.Send message
        member __.AddConnectionFailedHandler lt handler =
            let handlerDelegate = XmppClient.ConnectionFailedHandler(fun _ args -> handler args)
            client.add_ConnectionFailed handlerDelegate
            lt.OnTermination(fun () -> client.remove_ConnectionFailed handlerDelegate) |> ignore
        member __.AddPresenceHandler lt handler =
            let handlerDelegate = XmppClient.PresenceHandler(fun _ args -> handler args)
            client.add_Presence handlerDelegate
            lt.OnTermination(fun () -> client.remove_Presence handlerDelegate) |> ignore
        member __.AddMessageHandler lt handler =
            let handlerDelegate = XmppClient.MessageHandler(fun _ args -> handler args)
            client.add_Message handlerDelegate
            lt.OnTermination(fun () -> client.remove_Message handlerDelegate) |> ignore

// TODO[F]: This client should be removed
// TODO[F]: But preserve the logging routines; they're good
let private connectionFailedHandler (logger: ILogger) = XmppConnection.ConnectionFailedHandler(fun s e ->
    logger.Error(e.Exception, "XMPP connection failed: {Message}", e.Message)
    ())

let private signedInHandler (logger: ILogger) (settings: XmppSettings) (client: XmppClient) =
    XmppConnection.SignedInHandler(fun s e ->
        logger.Information("Connecting to {Room} as {Nickname}", settings.Room, settings.Nickname)
        SharpXmppHelper.joinRoom client settings.Room settings.Nickname
    )

let private shouldProcessMessage settings message =
    let isGroup = SharpXmppHelper.isGroupChatMessage message
    let shouldSkip = lazy (
        SharpXmppHelper.isOwnMessage (settings.Nickname) message
        || SharpXmppHelper.isHistoricalMessage message
        || SharpXmppHelper.isEmptyMessage message
    )
    isGroup && not shouldSkip.Value

let private messageHandler (logger: ILogger) settings onMessage = XmppConnection.MessageHandler(fun _ element ->
    logger.Verbose("Incoming XMPP message: {Message}", element)
    if shouldProcessMessage settings element then
        onMessage(XmppMessage (SharpXmppHelper.parseMessage element))
)

let private elementHandler (logger: ILogger) = XmppConnection.ElementHandler(fun s e ->
    let direction = if e.IsInput then "incoming" else "outgoing"
    logger.Verbose("XMPP stanza ({Direction}): {Stanza}", direction, e.Stanza)
)

let private presenceHandler (logger: ILogger) = XmppConnection.PresenceHandler(fun s e ->
    logger.Verbose("XMPP presence: {Presence}", e)
)

let create (logger: ILogger) (settings: XmppSettings) (onMessage: IncomingMessage -> unit): XmppClient =
    let client = new XmppClient(JID(settings.Login), settings.Password)
    client.add_ConnectionFailed(connectionFailedHandler logger)
    client.add_SignedIn(signedInHandler logger settings client)
    client.add_Element(elementHandler logger)
    client.add_Presence(presenceHandler logger)
    client.add_Message(messageHandler logger settings onMessage)
    client

type ConnectionFailedError(message: string, innerException: Exception) =
    inherit Exception(message, innerException)

let run (logger: ILogger) (client: XmppClient): Async<unit> =
    logger.Information("Running XMPP bot: {Jid}", client.Jid.FullJid)
    let connectionFinished = TaskCompletionSource()
    let connectionFailedHandler =
        XmppConnection.ConnectionFailedHandler(
            fun _ error -> connectionFinished.SetException(ConnectionFailedError(error.Message, error.Exception))
        )

    async {
        try
            let! token = Async.CancellationToken
            use _ = token.Register(fun () -> client.Close())

            client.add_ConnectionFailed connectionFailedHandler
            do! Async.AwaitTask(client.ConnectAsync token)

            do! Async.AwaitTask connectionFinished.Task
        finally
            client.remove_ConnectionFailed connectionFailedHandler
    }

let send (settings: XmppSettings) (client: XmppClient) (message: Message): unit =
    let text = sprintf "<%s> %s" message.author message.text
    SharpXmppHelper.message None settings.Room text
    |> client.Send
