module Emulsion.Actors.Telegram

open Akka.Actor

open Emulsion
open Emulsion.Settings

type TelegramActor(core : IActorRef, settings : TelegramSettings) as this =
    inherit SyncTaskWatcher()
    do printfn "Starting Telegram actor..."
    do this.Receive<string>(this.OnMessage)

    override this.RunInTask() =
        printfn "Starting Telegram connection..."
        Telegram.run settings (fun message -> core.Tell(TelegramMessage message))

    member private __.OnMessage(message : string) : unit =
        Telegram.send settings message

let spawn (settings : TelegramSettings) (factory : IActorRefFactory) (core : IActorRef) (name : string) : IActorRef =
    printfn "Spawning Telegram..."
    let props = Props.Create<TelegramActor>(core, settings)
    factory.ActorOf(props, name)
