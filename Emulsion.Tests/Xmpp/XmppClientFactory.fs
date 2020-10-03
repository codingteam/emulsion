namespace Emulsion.Tests.Xmpp

open Emulsion.Xmpp.XmppClient

type XmppClientFactory =
    static member create(?connect,
                         ?joinMultiUserChat,
                         ?send,
                         ?addConnectionFailedHandler,
                         ?addIqHandler,
                         ?addPresenceHandler,
                         ?addMessageHandler): IXmppClient =
        let connect = defaultArg connect <| fun () -> async { return () }
        let joinMultiUserChat = defaultArg joinMultiUserChat <| fun _ _ _ -> ()
        let send = defaultArg send ignore
        let addConnectionFailedHandler = defaultArg addConnectionFailedHandler <| fun _ _ -> ()
        let addIqHandler = defaultArg addIqHandler <| fun _ _ -> ()
        let addPresenceHandler = defaultArg addPresenceHandler <| fun _ _ -> ()
        let addMessageHandler = defaultArg addMessageHandler <| fun _ _ -> ()
        { new IXmppClient with
            member _.Connect() = connect()
            member _.JoinMultiUserChat roomJid nickname password = joinMultiUserChat roomJid nickname password
            member _.Send m = send m
            member _.AddConnectionFailedHandler lt handler = addConnectionFailedHandler lt handler
            member _.AddSignedInHandler _ _ = ()
            member _.AddElementHandler _ _ = ()
            member _.AddIqHandler lt handler = addIqHandler lt handler
            member _.AddPresenceHandler lt handler = addPresenceHandler lt handler
            member _.AddMessageHandler lt handler = addMessageHandler lt handler
        }
