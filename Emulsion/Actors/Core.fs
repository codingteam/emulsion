module Emulsion.Actors.Core

open Akka.Actor
open Serilog

open Emulsion
open Emulsion.Telegram

type CoreActor(logger: ILogger, factories: ActorFactories) as this =
    inherit ReceiveActor()

    do this.Receive<IncomingMessage>(this.OnMessage)
    let mutable xmpp = Unchecked.defaultof<IActorRef>
    let mutable telegram = Unchecked.defaultof<IActorRef>

    member private this.spawn (factory : ActorFactory) name =
        factory ActorBase.Context name

    override this.PreStart() =
        logger.Information "Core actor starting…"
        xmpp <- this.spawn factories.xmppFactory "xmpp"
        telegram <- this.spawn factories.telegramFactory "telegram"

    member this.OnMessage(message : IncomingMessage) : unit =
        match message with
        | TelegramMessage msg ->
            let message = Funogram.MessageConverter.flatten Funogram.MessageConverter.DefaultQuoteSettings msg
            xmpp.Tell(OutgoingMessage message, this.Self)
        | XmppMessage msg -> telegram.Tell(OutgoingMessage msg, this.Self)

let spawn (logger: ILogger) (factories: ActorFactories) (system: IActorRefFactory) (name: string): IActorRef =
    logger.Information "Core actor spawning…"
    let props = Props.Create<CoreActor>(logger, factories)
    system.ActorOf(props, name)
