module Emulsion.Xmpp.XmppClient

open System.Threading.Tasks

open SharpXMPP
open SharpXMPP.XMPP

open Emulsion
open Emulsion.Settings

let private connectionFailedHandler = XmppConnection.ConnectionFailedHandler(fun s e ->
    printfn "XMPP Connection Failed: %s" e.Message
    ())

let private signedInHandler (settings : XmppSettings) (client : XmppClient) = XmppConnection.SignedInHandler(fun s e ->
    printfn "Connecting to %s" settings.room
    SharpXmppHelper.joinRoom client settings.room settings.nickname)

let private shouldSkipMessage settings message =
    SharpXmppHelper.isOwnMessage (settings.nickname) message
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

let create (settings: XmppSettings): XmppClient =
    let client = XmppClient(JID(settings.login), settings.password)
    client.add_ConnectionFailed(connectionFailedHandler)
    client.add_SignedIn(signedInHandler settings client)
    client.add_Element(elementHandler)
    client.add_Presence(presenceHandler)
    client

exception ConnectionFailedError of string
    with
        override this.ToString() =
            sprintf "%A" this

let run (settings: XmppSettings) (client: XmppClient) (onMessage: IncomingMessage -> unit): Async<unit> =
    printfn "Bot name: %s" client.Jid.FullJid
    let handler = messageHandler settings onMessage
    let tcs = TaskCompletionSource()
    let connectionFailedHandler =
        XmppConnection.ConnectionFailedHandler(
            fun _ error -> tcs.SetException(ConnectionFailedError error.Message)
        )

    async {
        try
            let! token = Async.CancellationToken
            use _ = token.Register(fun () -> client.Close())

            client.add_Message handler
            client.add_ConnectionFailed connectionFailedHandler
            do! Async.AwaitTask(client.ConnectAsync token)

            do! Async.AwaitTask tcs.Task
        finally
            client.remove_ConnectionFailed connectionFailedHandler
            client.remove_Message handler
    }

let send (settings: XmppSettings) (client: XmppClient) (message: Message): unit =
    let text = sprintf "<%s> %s" message.author message.text
    SharpXmppHelper.message settings.room text
    |> client.Send
