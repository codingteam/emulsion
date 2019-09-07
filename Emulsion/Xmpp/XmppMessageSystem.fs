namespace Emulsion.Xmpp

open System.Threading

open JetBrains.Lifetimes

open Emulsion
open Emulsion.MessageSystem
open Emulsion.Settings

type XmppMessageSystem(ctx: ServiceContext, cancellationToken: CancellationToken, settings: XmppSettings) =
    inherit MessageSystemBase(ctx, cancellationToken)

    let client = ref None

    override __.RunUntilError receiver = async {
        use sharpXmpp = SharpXmppClient.create settings
        let newClient = SharpXmppClient.wrap sharpXmpp |> EmulsionXmpp.initializeLogging ctx.Logger
        use newClientLifetimeDef = Lifetime.Define()
        try
            Volatile.Write(client, Some (newClient, newClientLifetimeDef.Lifetime))
            do! EmulsionXmpp.run settings ctx.Logger newClient receiver
        finally
            Volatile.Write(client, None)
    }

    override __.Send (OutgoingMessage message) = async {
         match Volatile.Read(client) with
         | None -> failwith "Client is offline"
         | Some (client, lt) ->
             return! EmulsionXmpp.send ctx.Logger client lt settings message
    }
