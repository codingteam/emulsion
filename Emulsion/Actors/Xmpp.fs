module Emulsion.Actors.Xmpp

open Akka.Actor
open Serilog

open Emulsion.Messaging
open Emulsion.Messaging.MessageSystem

type XmppActor(logger: ILogger, xmpp: IMessageSystem) as this =
    inherit ReceiveActor()
    do logger.Information("XMPP actor starting ({Path})…", this.Self.Path)
    do this.Receive<OutgoingMessage>(putMessage xmpp)

let spawn (logger: ILogger)
          (xmpp: IMessageSystem)
          (factory: IActorRefFactory)
          (name: string): IActorRef =
    logger.Information("XMPP actor spawning…")
    let props = Props.Create<XmppActor>(logger, xmpp)
    factory.ActorOf(props, name)
