module Emulsion.Actors.Telegram

open Akka.Actor

open Emulsion
open Emulsion.Telegram

type TelegramActor(core : IActorRef, telegram : Client) as this =
    inherit SyncTaskWatcher()
    do printfn "Starting Telegram actor (%A)..." this.Self.Path
    do this.Receive<OutgoingMessage>(this.OnMessage)

    override this.RunInTask() =
        printfn "Starting Telegram connection..."
        telegram.run (fun message -> core.Tell(TelegramMessage message))

    member private __.OnMessage message : unit =
        telegram.send message

let spawn (telegram : Client)
          (factory : IActorRefFactory)
          (core : IActorRef)
          (name : string) : IActorRef =
    printfn "Spawning Telegram..."
    let props = Props.Create<TelegramActor>(core, telegram)
    factory.ActorOf(props, name)
