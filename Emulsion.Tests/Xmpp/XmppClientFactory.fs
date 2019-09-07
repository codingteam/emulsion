namespace Emulsion.Tests.Xmpp

open Emulsion.Xmpp.XmppClient

type XmppClientFactory =
    static member create(?connect, ?addConnectionFailedHandler, ?addPresenceHandler, ?joinMultiUserChat): IXmppClient =
        let connect = defaultArg connect <| fun () -> async { return () }
        let addConnectionFailedHandler = defaultArg addConnectionFailedHandler <| fun _ _ -> ()
        let addPresenceHandler = defaultArg addPresenceHandler <| fun _ _ -> ()
        let joinMultiUserChat = defaultArg joinMultiUserChat <| fun _ _ -> ()
        { new IXmppClient with
            member __.Connect() = connect()
            member __.AddConnectionFailedHandler lt handler = addConnectionFailedHandler lt handler
            member __.AddPresenceHandler lt handler = addPresenceHandler lt handler
            member __.JoinMultiUserChat roomJid nickname = joinMultiUserChat roomJid nickname
        }
