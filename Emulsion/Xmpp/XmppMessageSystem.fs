namespace Emulsion.Xmpp

open System.Threading

open JetBrains.Lifetimes

open Emulsion
open Emulsion
open Emulsion.MessageSystem
open Emulsion.Settings

type XmppMessageSystem(ctx: ServiceContext, cancellationToken: CancellationToken, settings: XmppSettings) =
    inherit MessageSystemBase(ctx, cancellationToken)

    let client = ref None

    member private __.BaseRunAsync r = base.RunAsync r

    override this.RunAsync receiver = async {
        // This overload essentially wraps a base method with a couple of "use" statements.
        use sharpXmpp = SharpXmppClient.create settings
        let newClient = SharpXmppClient.wrap sharpXmpp |> EmulsionXmpp.initializeLogging ctx.Logger
        use newClientLifetimeDef = Lifetime.Define()
        try
            Volatile.Write(client, Some (newClient, newClientLifetimeDef.Lifetime))
            do! this.BaseRunAsync receiver
        finally
            Volatile.Write(client, None)
    }

    override _.RunUntilError receiver = async {
        match !client with
        | Some(client, _) -> return! EmulsionXmpp.run settings ctx.Logger client receiver
        | _ -> return failwith "The system cannot be run: the connection is not established"
    }

    override _.Send (OutgoingMessage message) = async {
         match Volatile.Read(client) with
         | None -> failwith "Client is offline"
         | Some (client, lt) ->
             return! EmulsionXmpp.send ctx.Logger client lt settings message
    }
