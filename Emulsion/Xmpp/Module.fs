[<AutoOpen>]
module Emulsion.Xmpp.Module

open System

open Akka.Actor
open SharpXMPP

open Emulsion
open Emulsion.Settings

type XmppModule =
    { construct : IActorRef -> XmppClient
      run : XmppClient -> unit
      send : XmppClient -> string -> unit }

let private construct settings (core : IActorRef) =
    XmppClient.create settings core.Tell

let xmppModule (settings : XmppSettings) : XmppModule =
    { construct = construct settings
      run = XmppClient.run
      send = XmppClient.send settings }
