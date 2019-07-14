module Emulsion.Actors.Core

open Akka.Actor

open Emulsion
open Emulsion.Telegram

type CoreActor(factories : ActorFactories) as this =
    inherit ReceiveActor()

    do this.Receive<IncomingMessage>(this.OnMessage)
    let mutable xmpp = Unchecked.defaultof<IActorRef>
    let mutable telegram = Unchecked.defaultof<IActorRef>

    member private this.spawn (factory : ActorFactory) name =
        factory ActorBase.Context name

    override this.PreStart() =
        printfn "Starting Core actor..."
        xmpp <- this.spawn factories.xmppFactory "xmpp"
        telegram <- this.spawn factories.telegramFactory "telegram"

    member this.OnMessage(message : IncomingMessage) : unit =
        match message with
        | TelegramMessage msg ->
            let message = Funogram.MessageConverter.flatten Funogram.MessageConverter.DefaultQuoteSettings msg
            xmpp.Tell(OutgoingMessage message, this.Self)
        | XmppMessage msg -> telegram.Tell(OutgoingMessage msg, this.Self)

let spawn (factories : ActorFactories) (system : IActorRefFactory) (name : string) : IActorRef =
    printfn "Spawning Core..."
    let props = Props.Create<CoreActor>(factories)
    system.ActorOf(props, name)
