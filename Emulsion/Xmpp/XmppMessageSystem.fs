namespace Emulsion.Xmpp

open System.Threading

open Emulsion
open Emulsion.MessageSystem
open Emulsion.Settings
open Emulsion.Xmpp.XmppClient

type XmppMessageSystem(ctx: ServiceContext, cancellationToken: CancellationToken, settings: XmppSettings) =
    inherit MessageSystemBase(ctx, cancellationToken)

    let client = ref None

    override __.RunUntilError receiver = async {
        use sharpXmpp = SharpXmppClient.create settings
        let newClient = SharpXmppClient.wrap sharpXmpp |> EmulsionXmpp.initializeLogging ctx.Logger
        try
            Volatile.Write(client, Some newClient)
            do! EmulsionXmpp.run settings ctx.Logger newClient receiver
        finally
            Volatile.Write(client, None)
    }

    override __.Send (OutgoingMessage message) = async {
         match Volatile.Read(client) with
         | None -> failwith "Client is offline"
         | Some client ->
            return EmulsionXmpp.send settings client message
    }
