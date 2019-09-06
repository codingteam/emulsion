namespace Emulsion.Tests.Xmpp

open Emulsion.Xmpp.XmppClient

type XmppClientFactory =
    static member create(?connect, ?addConnectionFailedHandler): IXmppClient =
        let connect = defaultArg connect <| fun () -> async { return () }
        let addConnectionFailedHandler = defaultArg addConnectionFailedHandler <| fun _ _ -> ()
        { new IXmppClient with
            member __.Connect() = connect()
            member __.AddConnectionFailedHandler lt handler = addConnectionFailedHandler lt handler
        }
