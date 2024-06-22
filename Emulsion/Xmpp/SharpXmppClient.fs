// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

/// An implementation of an IXmppClient based on SharpXMPP library.
module Emulsion.Xmpp.SharpXmppClient

open SharpXMPP
open SharpXMPP.XMPP

open Emulsion.Xmpp
open Emulsion.Xmpp.XmppClient
open Emulsion.Settings

type Wrapper(client: XmppClient) =
    let socketWriterLock = obj()

    interface IXmppClient with
        member _.Connect() = async {
            let! ct = Async.CancellationToken
            return! Async.AwaitTask(client.ConnectAsync ct)
        }
        member _.JoinMultiUserChat roomJid nickname password = SharpXmppHelper.joinRoom client roomJid.BareJid nickname password
        member _.Send message =
            lock socketWriterLock (fun () ->
                client.Send message
            )
        member this.SendIqQuery lt iq handler =
            lock socketWriterLock (fun () ->
                client.Query(iq, fun response -> lt.Execute(fun() -> handler response))
            )
        member _.AddSignedInHandler lt handler =
            let handlerDelegate = XmppClient.SignedInHandler(fun _ -> handler)
            client.add_SignedIn handlerDelegate
            lt.OnTermination(fun () -> client.remove_SignedIn handlerDelegate) |> ignore
        member _.AddElementHandler lt handler =
            let handlerDelegate = XmppClient.ElementHandler(fun _ -> handler)
            client.add_Element handlerDelegate
            lt.OnTermination(fun () -> client.remove_Element handlerDelegate) |> ignore
        member _.AddConnectionFailedHandler lt handler =
            let handlerDelegate = XmppClient.ConnectionFailedHandler(fun _ -> handler)
            client.add_ConnectionFailed handlerDelegate
            lt.OnTermination(fun () -> client.remove_ConnectionFailed handlerDelegate) |> ignore
        member _.AddPresenceHandler lt handler =
            let handlerDelegate = XmppClient.PresenceHandler(fun _ -> handler)
            client.add_Presence handlerDelegate
            lt.OnTermination(fun () -> client.remove_Presence handlerDelegate) |> ignore
        member _.AddMessageHandler lt handler =
            let handlerDelegate = XmppClient.MessageHandler(fun _ -> handler)
            client.add_Message handlerDelegate
            lt.OnTermination(fun () -> client.remove_Message handlerDelegate) |> ignore

let create (settings: XmppSettings): XmppClient =
    let client = new XmppClient(JID(settings.Login), settings.Password)
    client.IqManager.PayloadHandlers.Add(SharpXmppPingHandler())
    client

let wrap(client: XmppClient): IXmppClient =
    Wrapper client
