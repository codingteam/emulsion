module Emulsion.Actors.Telegram

open Akka.Actor

open Emulsion
open Emulsion.MessageSystem

type TelegramActor(telegram: IMessageSystem) as this =
    inherit ReceiveActor()
    do printfn "Starting Telegram actor (%A)..." this.Self.Path
    do this.Receive<OutgoingMessage>(MessageSystem.putMessage telegram)

let spawn (telegram: IMessageSystem) (factory: IActorRefFactory) (name: string): IActorRef =
    printfn "Spawning Telegram..."
    let props = Props.Create<TelegramActor>(telegram)
    factory.ActorOf(props, name)
