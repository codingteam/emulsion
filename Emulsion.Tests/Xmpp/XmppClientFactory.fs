namespace Emulsion.Tests.Xmpp

open Emulsion.Xmpp.XmppClient

type XmppClientFactory =
    static member create(?connect,
                         ?joinMultiUserChat,
                         ?send,
                         ?addConnectionFailedHandler,
                         ?addPresenceHandler,
                         ?addMessageHandler): IXmppClient =
        let connect = defaultArg connect <| fun () -> async { return () }
        let joinMultiUserChat = defaultArg joinMultiUserChat <| fun _ _ -> ()
        let send = defaultArg send ignore
        let addConnectionFailedHandler = defaultArg addConnectionFailedHandler <| fun _ _ -> ()
        let addPresenceHandler = defaultArg addPresenceHandler <| fun _ _ -> ()
        let addMessageHandler = defaultArg addMessageHandler <| fun _ _ -> ()
        { new IXmppClient with
            member __.Connect() = connect()
            member __.JoinMultiUserChat roomJid nickname = joinMultiUserChat roomJid nickname
            member __.Send m = send m
            member __.AddConnectionFailedHandler lt handler = addConnectionFailedHandler lt handler
            member __.AddSignedInHandler _ _ = ()
            member __.AddElementHandler _ _ = ()
            member __.AddPresenceHandler lt handler = addPresenceHandler lt handler
            member __.AddMessageHandler lt handler = addMessageHandler lt handler
        }
