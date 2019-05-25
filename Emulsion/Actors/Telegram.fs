module Emulsion.Actors.Telegram

open Akka.Actor

open Emulsion
open Emulsion.Settings
open Emulsion.Telegram

type TelegramActor(core : IActorRef, settings : TelegramSettings, telegram : Client) as this =
    inherit SyncTaskWatcher()
    do printfn "Starting Telegram actor (%A)..." this.Self.Path
    do this.Receive<OutgoingMessage>(this.OnMessage)

    override this.RunInTask() =
        printfn "Starting Telegram connection..."
        telegram.run settings (fun message -> core.Tell(TelegramMessage message))

    member private __.OnMessage message : unit =
        telegram.send settings message

let spawn (settings : TelegramSettings)
          (telegram : Client)
          (factory : IActorRefFactory)
          (core : IActorRef)
          (name : string) : IActorRef =
    printfn "Spawning Telegram..."
    let props = Props.Create<TelegramActor>(core, settings, telegram)
    factory.ActorOf(props, name)
