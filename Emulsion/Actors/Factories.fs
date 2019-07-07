[<AutoOpen>]
module Emulsion.Actors.Factories

open Akka.Actor

type ActorFactory = IActorRefFactory -> string -> IActorRef
type ActorFactories =
    { xmppFactory : ActorFactory
      telegramFactory : ActorFactory }
