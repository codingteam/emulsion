module Emulsion.Actors.Xmpp

open Akka.Actor

open Emulsion
open Emulsion.MessageSystem

type XmppActor(xmpp: IMessageSystem) as this =
    inherit ReceiveActor()
    do printfn "Starting XMPP actor (%A)..." this.Self.Path
    do this.Receive<OutgoingMessage>(MessageSystem.putMessage xmpp)

let spawn (xmpp: IMessageSystem)
          (factory: IActorRefFactory)
          (name: string) =
    printfn "Spawning XMPP..."
    let props = Props.Create<XmppActor>(xmpp)
    factory.ActorOf(props, name)
