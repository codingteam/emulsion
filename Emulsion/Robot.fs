namespace Emulsion.Xmpp

open System
open System.Threading
open System.Xml.Linq

open SharpXMPP
open SharpXMPP.XMPP

open Emulsion.Settings

type Robot(logger: string -> unit,
           settings : XmppSettings) =
    let { login = login
          room = roomJid
          password = password
          nickname = nickname } = settings
    do logger <| sprintf "Bot name: %s; connecting to: %s"  login roomJid
    let connection = XmppClient(JID(login), password)

    let connectionFailedHandler = XmppConnection.ConnectionFailedHandler(fun s e ->
        logger <| e.Message
        Thread.Sleep(TimeSpan.FromSeconds(30.0)) // TODO[Friedrich]: Configurable timeout.
        ())

    let signedInHandler = XmppConnection.SignedInHandler(fun s e ->
        logger <| "Connecting to " + roomJid
        SharpXmppHelper.joinRoom connection roomJid nickname)

    let messageHandler = XmppConnection.MessageHandler(fun s e ->
        logger <| "<-" + e.ToString()
        let x = e.Element(XNamespace.Get("jabber:client") + "subject")
        if not (isNull x) then
            logger <| "!!! Room"
            let msg = SharpXmppHelper.message roomJid "хнарл"
            connection.Send msg)

    let elementHandler = XmppConnection.ElementHandler(fun s e ->
        let arrow = if e.IsInput then "<-" else "->"
        logger <| arrow + " " + e.Stanza.ToString())

    let presenceHandler = XmppConnection.PresenceHandler(fun s e ->
        logger <| "[P]:" + e.ToString())

    do
        connection.add_ConnectionFailed(connectionFailedHandler)
        connection.add_SignedIn(signedInHandler)
        connection.add_Message(messageHandler)
        connection.add_Element(elementHandler)
        connection.add_Presence(presenceHandler)
        connection.Connect()

    interface IDisposable with
        member __.Dispose() = connection.Close()
