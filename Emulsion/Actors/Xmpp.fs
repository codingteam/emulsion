module Emulsion.Actors.Xmpp

open Akka.Actor

open Emulsion
open Emulsion.Xmpp

type XmppActor(core : IActorRef, xmpp : Client) as this =
    inherit SyncTaskWatcher()
    do printfn "Starting XMPP actor (%A)..." this.Self.Path
    do this.Receive<OutgoingMessage>(this.OnMessage)
    let robot = xmpp.construct core

    override __.RunInTask() =
        printfn "Starting XMPP connection..."
        xmpp.run robot

    member private __.OnMessage(OutgoingMessage { author = author; text = text }) : unit =
        let msg = sprintf "<%s> %s" author text
        xmpp.send robot msg

let spawn (xmpp : Client)
          (factory : IActorRefFactory)
          (core : IActorRef)
          (name : string) =
    printfn "Spawning XMPP..."
    let props = Props.Create<XmppActor>(core, xmpp)
    factory.ActorOf(props, name)
