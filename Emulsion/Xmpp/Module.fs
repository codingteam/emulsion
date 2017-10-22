[<AutoOpen>]
module Emulsion.Xmpp.Module

open System
open Akka.Actor

open Emulsion
open Emulsion.Settings

type XmppModule =
    { construct : XmppSettings -> IActorRef -> Robot
      run : Robot -> unit
      send : Robot -> string -> unit }

let private construct settings (core : IActorRef) =
    new Robot(Console.WriteLine, settings, fun message -> core.Tell(XmppMessage message))

let xmppModule : XmppModule =
    { construct = construct
      run = fun r -> r.Run()
      send = fun r m -> r.PublishMessage(m) }
