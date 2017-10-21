module Emulsion.Core

open Akka.Actor
open Akka.FSharp

let spawn (system : IActorRefFactory) : IActorRef =
    let xmpp = select "akka://emulsion/user/xmpp" system
    let telegram = select "akka://emulsion/user/telegram" system
    let actor = actorOf (function | XmppMessage _ as msg -> telegram <! msg
                                  | TelegramMessage _ as msg -> xmpp <! msg)
    spawn system "core" actor
