/// An implementation of an IXmppClient based on SharpXMPP library.
module Emulsion.Xmpp.SharpXmppClient

open SharpXMPP
open SharpXMPP.XMPP

open Emulsion.Xmpp
open Emulsion.Xmpp.XmppClient
open Emulsion.Settings

type Wrapper(client: XmppClient) =
    interface IXmppClient with
        member _.Connect() = async {
            let! ct = Async.CancellationToken
            return! Async.AwaitTask(client.ConnectAsync ct)
        }
        member _.JoinMultiUserChat roomJid nickname = SharpXmppHelper.joinRoom client roomJid.BareJid nickname
        member _.Send message = client.Send message
        member _.AddSignedInHandler lt handler =
            let handlerDelegate = XmppClient.SignedInHandler(fun _ args -> handler args)
            client.add_SignedIn handlerDelegate
            lt.OnTermination(fun () -> client.remove_SignedIn handlerDelegate) |> ignore
        member _.AddElementHandler lt handler =
            let handlerDelegate = XmppClient.ElementHandler(fun _ args -> handler args)
            client.add_Element handlerDelegate
            lt.OnTermination(fun () -> client.remove_Element handlerDelegate) |> ignore
        member _.AddConnectionFailedHandler lt handler =
            let handlerDelegate = XmppClient.ConnectionFailedHandler(fun _ args -> handler args)
            client.add_ConnectionFailed handlerDelegate
            lt.OnTermination(fun () -> client.remove_ConnectionFailed handlerDelegate) |> ignore
        member _.AddPresenceHandler lt handler =
            let handlerDelegate = XmppClient.PresenceHandler(fun _ args -> handler args)
            client.add_Presence handlerDelegate
            lt.OnTermination(fun () -> client.remove_Presence handlerDelegate) |> ignore
        member _.AddMessageHandler lt handler =
            let handlerDelegate = XmppClient.MessageHandler(fun _ args -> handler args)
            client.add_Message handlerDelegate
            lt.OnTermination(fun () -> client.remove_Message handlerDelegate) |> ignore

let create (settings: XmppSettings): XmppClient =
    new XmppClient(JID(settings.Login), settings.Password)

let wrap(client: XmppClient): IXmppClient =
    upcast Wrapper client
