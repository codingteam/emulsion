module Emulsion.Xmpp.XmppClient

open System
open System.Threading.Tasks

open SharpXMPP
open SharpXMPP.XMPP

open Emulsion
open Emulsion.Settings

let private connectionFailedHandler = XmppConnection.ConnectionFailedHandler(fun s e ->
    printfn "XMPP Connection Failed: %s" e.Message
    ())

let private signedInHandler (settings : XmppSettings) (client : XmppClient) = XmppConnection.SignedInHandler(fun s e ->
    printfn "Connecting to %s" settings.Room
    SharpXmppHelper.joinRoom client settings.Room settings.Nickname)

let private shouldSkipMessage settings message =
    SharpXmppHelper.isOwnMessage (settings.Nickname) message
        || SharpXmppHelper.isHistoricalMessage message

let private messageHandler settings onMessage = XmppConnection.MessageHandler(fun _ element ->
    printfn "<- %A" element
    if not <| shouldSkipMessage settings element then
        onMessage(XmppMessage (SharpXmppHelper.parseMessage element))
)

let private elementHandler = XmppConnection.ElementHandler(fun s e ->
    let arrow = if e.IsInput then "<-" else "->"
    printfn "%s %A" arrow e.Stanza)

let private presenceHandler = XmppConnection.PresenceHandler(fun s e ->
    printfn "[P]: %A" e)

let create (settings: XmppSettings) (onMessage: IncomingMessage -> unit): XmppClient =
    let client = new XmppClient(JID(settings.Login), settings.Password)
    client.add_ConnectionFailed(connectionFailedHandler)
    client.add_SignedIn(signedInHandler settings client)
    client.add_Element(elementHandler)
    client.add_Presence(presenceHandler)
    client.add_Message(messageHandler settings onMessage)
    client

type ConnectionFailedError(message: string, innerException: Exception) =
    inherit Exception(message, innerException)

let run (client: XmppClient): Async<unit> =
    printfn "Bot name: %s" client.Jid.FullJid
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
    SharpXmppHelper.message settings.Room text
    |> client.Send
