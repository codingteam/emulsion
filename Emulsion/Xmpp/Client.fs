namespace Emulsion.Xmpp

open System.Threading

open Emulsion
open Emulsion.MessageSystem
open Emulsion.Settings

type Client(ctx: ServiceContext, cancellationToken: CancellationToken, settings: XmppSettings) =
    inherit MessageSystemBase(ctx, cancellationToken)

    let client = ref None

    override __.RunUntilError receiver = async {
        use newClient = XmppClient.create settings receiver
        try
            Volatile.Write(client, Some newClient)
            do! XmppClient.run newClient
        finally
            Volatile.Write(client, None)
    }

    override __.Send (OutgoingMessage message) = async {
         match Volatile.Read(client) with
         | None -> failwith "Client is offline"
         | Some client ->
            return XmppClient.send settings client message
    }
