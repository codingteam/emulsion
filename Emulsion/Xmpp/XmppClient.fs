module Emulsion.Xmpp.XmppClient

open System
open System.Threading
open System.Xml.Linq

open SharpXMPP
open SharpXMPP.XMPP

open Emulsion
open Emulsion.Settings

let private connectionFailedHandler = XmppConnection.ConnectionFailedHandler(fun s e ->
    printfn "%s" e.Message
    Thread.Sleep(TimeSpan.FromSeconds(30.0)) // TODO[Friedrich]: Configurable timeout.
    ())

let private signedInHandler (settings : XmppSettings) (client : XmppClient) = XmppConnection.SignedInHandler(fun s e ->
    printfn "Connecting to %s" settings.room
    SharpXmppHelper.joinRoom client settings.room settings.nickname)

let private messageHandler onMessage = XmppConnection.MessageHandler(fun s e ->
    printfn "<- %A" e
    let x = e.Element(XNamespace.Get("jabber:client") + "subject")
    onMessage(e.ToString()))

let private elementHandler = XmppConnection.ElementHandler(fun s e ->
    let arrow = if e.IsInput then "<-" else "->"
    printfn "%s %A" arrow e.Stanza)

let private presenceHandler = XmppConnection.PresenceHandler(fun s e ->
    printfn "[P]: %A" e)

let create (settings : XmppSettings) (onMessage : string -> unit) : XmppClient =
    let client = XmppClient(JID(settings.login), settings.password)
    client.add_ConnectionFailed(connectionFailedHandler)
    client.add_SignedIn(signedInHandler settings client)
    client.add_Message(messageHandler onMessage)
    client.add_Element(elementHandler)
    client.add_Presence(presenceHandler)
    client

let run (client : XmppClient) =
    printfn "Bot name: %s" client.Jid.FullJid
    client.Connect()

let send (settings : XmppSettings) (client : XmppClient) (message : string) : unit =
    SharpXmppHelper.message settings.room message
    |> client.Send
