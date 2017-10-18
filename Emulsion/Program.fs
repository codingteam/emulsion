module Emulsion.Program

open System
open System.IO

open Akka.FSharp
open Microsoft.Extensions.Configuration

open Emulsion.Settings
open Emulsion.Xmpp

let private startXmpp settings = async {
    printfn "Starting XMPP"
    ignore (new Robot(Console.WriteLine, settings))
}

let private startTelegram settings = async {
    printfn "Starting Telegram"
    Telegram.run settings
}

let private getConfiguration directory fileName =
    let config =
        ConfigurationBuilder()
            .SetBasePath(directory)
            .AddJsonFile(fileName)
            .Build()
    Settings.read config

let private xmppActor system config =
    let robot = startXmpp config |> Async.Start
    actorOf (fun msg -> ())

let private telegramActor system config =
    let robot = startTelegram config |> Async.Start
    actorOf (fun msg -> ())

let private startApp config =
    async {
        use system = System.create "emulsion" (Configuration.defaultConfig())
        let xmpp = spawn system "xmpp" (xmppActor system config.xmpp)
        let telegram = spawn system "telegram" (telegramActor system config.telegram)
        do! Async.AwaitTask system.WhenTerminated
    }

let private runApp app =
    Async.RunSynchronously app
    0

let private defaultConfigFileName = "emulsion.json"

[<EntryPoint>]
let main = function
    | [| |] ->
        getConfiguration (Directory.GetCurrentDirectory()) defaultConfigFileName
        |> startApp
        |> runApp
    | [| configPath |] ->
        getConfiguration (Path.GetDirectoryName configPath) (Path.GetFileName configPath)
        |> startApp
        |> runApp
    | _ ->
        printfn "Arguments: [config file name] (%s by default)" defaultConfigFileName
        0
