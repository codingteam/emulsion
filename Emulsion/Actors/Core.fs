module Emulsion.Actors.Core

open System
open System.Threading.Tasks

open Akka.Actor
open Serilog

open Emulsion
open Emulsion.Messaging

type CoreActor(logger: ILogger, factories: ActorFactories, archive: MessageArchive option) as this =
    inherit ReceiveActor()

    do this.ReceiveAsync<IncomingMessage>(Func<_, _> this.OnMessage)
    let mutable xmpp = Unchecked.defaultof<IActorRef>
    let mutable telegram = Unchecked.defaultof<IActorRef>

    member private this.spawn (factory : ActorFactory) name =
        factory ActorBase.Context name

    override this.PreStart() =
        logger.Information("Core actor starting ({Path})…", this.Self.Path)
        xmpp <- this.spawn factories.xmppFactory "xmpp"
        telegram <- this.spawn factories.telegramFactory "telegram"

    member this.OnMessage(message: IncomingMessage): Task =
        let self = this.Self
        task {
            match archive with
            | Some a -> do! a.Archive message
            | None -> ()

            match message with
            | TelegramMessage msg -> xmpp.Tell(OutgoingMessage msg, self)
            | XmppMessage msg -> telegram.Tell(OutgoingMessage msg, self)
        }

let spawn (logger: ILogger)
          (factories: ActorFactories)
          (system: IActorRefFactory)
          (archive: MessageArchive option)
          (name: string): IActorRef =
    logger.Information "Core actor spawning…"
    let props = Props.Create<CoreActor>(logger, factories, archive)
    system.ActorOf(props, name)
