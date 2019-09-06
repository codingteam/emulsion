namespace Emulsion.Xmpp

open System.Threading

open Emulsion
open Emulsion.MessageSystem
open Emulsion.Settings

// TODO[F]: Rename to an XmppMessageSystem?
type Client(ctx: ServiceContext, cancellationToken: CancellationToken, settings: XmppSettings) =
    inherit MessageSystemBase(ctx, cancellationToken)

    let client = ref None

    override __.RunUntilError receiver = async {
        use newClient = XmppClient.create ctx.Logger settings receiver
        try
            Volatile.Write(client, Some newClient)
            do! XmppClient.run ctx.Logger newClient
        finally
            Volatile.Write(client, None)
    }

    override __.Send (OutgoingMessage message) = async {
         match Volatile.Read(client) with
         | None -> failwith "Client is offline"
         | Some client ->
            return XmppClient.send settings client message
    }
