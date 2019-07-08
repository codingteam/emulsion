namespace Emulsion.Xmpp

open System.Threading

open Emulsion
open Emulsion.MessageSystem
open Emulsion.Settings

type Client(ctx: RestartContext, cancellationToken: CancellationToken, settings: XmppSettings) =
    inherit MessageSystemBase(ctx, cancellationToken)
    let client = XmppClient.create settings

    override __.RunUntilError receiver =
        XmppClient.run settings client receiver

    override __.Send (OutgoingMessage message) = async {
         return XmppClient.send settings client message
    }
