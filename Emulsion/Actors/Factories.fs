[<AutoOpen>]
module Emulsion.Actors.Factories

open Akka.Actor

type ActorFactory = IActorRefFactory -> IActorRef -> string -> IActorRef
type ActorFactories =
    { xmppFactory : ActorFactory
      telegramFactory : ActorFactory }
