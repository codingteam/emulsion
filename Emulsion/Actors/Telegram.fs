module Emulsion.Actors.Telegram

open Akka.Actor
open Serilog

open Emulsion.Messaging
open Emulsion.Messaging.MessageSystem

type TelegramActor(logger: ILogger, telegram: IMessageSystem) as this =
    inherit ReceiveActor()
    do logger.Information("Telegram actor starting ({Path})…", this.Self.Path)
    do this.Receive<OutgoingMessage>(putMessage telegram)

let spawn (logger: ILogger) (telegram: IMessageSystem) (factory: IActorRefFactory) (name: string): IActorRef =
    logger.Information("Telegram actor spawning…")
    let props = Props.Create<TelegramActor>(logger, telegram)
    factory.ActorOf(props, name)
