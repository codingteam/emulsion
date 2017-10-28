module Emulsion.Actors.Core

open Akka.Actor

open Emulsion

type CoreActor(factories : ActorFactories) as this =
    inherit ReceiveActor()

    do this.Receive<IncomingMessage>(this.OnMessage)
    let mutable xmpp = Unchecked.defaultof<IActorRef>
    let mutable telegram = Unchecked.defaultof<IActorRef>

    let toOutgoing = function
    | XmppMessage(author, text) -> OutgoingMessage(author, text)
    | TelegramMessage text ->  OutgoingMessage("Telegram user", text)

    member private this.spawn (factory : ActorFactory) name =
        factory ActorBase.Context this.Self name

    override this.PreStart() =
        printfn "Starting Core actor..."
        xmpp <- this.spawn factories.xmppFactory "xmpp"
        telegram <- this.spawn factories.telegramFactory "telegram"

    member this.OnMessage(message : IncomingMessage) : unit =
        match message with
        | TelegramMessage _ as msg -> xmpp.Tell(toOutgoing msg, this.Self)
        | XmppMessage _ as msg -> telegram.Tell(toOutgoing msg, this.Self)

let spawn (factories : ActorFactories) (system : IActorRefFactory) (name : string) : IActorRef =
    printfn "Spawning Core..."
    let props = Props.Create<CoreActor>(factories)
    system.ActorOf(props, name)
