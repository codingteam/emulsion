module Emulsion.Program

open System

open Ctor.Xmpp

let private startXmpp login password room = new Robot(Console.WriteLine, login, password, room, "хортолёт")
let private startTelegram token = Telegram.run token

[<EntryPoint>]
let main = function
    | [| login; password; room; token |] ->
        use xmpp = startXmpp login password room
        startTelegram token

        0
    | _ ->
        printfn "Arguments: login password room token"
        0
