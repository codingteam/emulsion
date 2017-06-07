module Emulsion.Program

open System

open Ctor.Xmpp

let private startXmpp login password room = new Robot(Console.WriteLine, login, password, room, "хортолёт")
let private startTelegram token = Telegram.run token

[<EntryPoint>]
let main [| login; password; room; token |] =
    use xmpp = startXmpp login password room
    let telegram = startTelegram
    Async.RunSynchronously <| telegram token

    0
