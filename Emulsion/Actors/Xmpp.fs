module Emulsion.Actors.Xmpp

open System

open Akka.Actor

open Emulsion
open Emulsion.Settings
open Emulsion.Xmpp

type XmppActor(core : IActorRef, settings : XmppSettings) as this =
    inherit SyncTaskWatcher()
    do printfn "Starting XMPP actor..."
    do this.Receive<string>(this.OnMessage)
    let robot = new Robot(Console.WriteLine, settings, fun message -> core.Tell(XmppMessage message))

    override __.RunInTask() =
        printfn "Starting XMPP connection..."
        robot.Run()

    member private __.OnMessage(message : string) : unit =
        robot.PublishMessage message

let spawn (settings : XmppSettings) (factory : IActorRefFactory) (core : IActorRef) (name : string) =
    printfn "Spawning XMPP..."
    let props = Props.Create<XmppActor>(core, settings)
    factory.ActorOf(props, name)
