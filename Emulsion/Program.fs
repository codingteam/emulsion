module Emulsion.Program

open System
open System.IO

open Akka.FSharp
open Microsoft.Extensions.Configuration

open Emulsion.Settings
open Emulsion.Xmpp

let private startXmpp (robot : Robot) = async {
    robot.Run()
}

let private startTelegram config handler = async {
    printfn "Starting Telegram"
    Telegram.run config handler
}

let private getConfiguration directory fileName =
    let config =
        ConfigurationBuilder()
            .SetBasePath(directory)
            .AddJsonFile(fileName)
            .Build()
    Settings.read config

let private xmppActor core settings =
    let robot = new Robot(Console.WriteLine, settings, fun msg -> core <! XmppMessage msg)
    startXmpp robot |> Async.Start
    actorOf (function | TelegramMessage x -> robot.PublishMessage x
                      | _ -> ())

let private telegramActor core config =
    startTelegram config (fun msg -> core <! TelegramMessage msg) |> Async.Start
    actorOf (function | XmppMessage x -> Telegram.send config x
                      | _ -> ())

let private startApp config =
    async {
        use system = System.create "emulsion" (Configuration.defaultConfig())
        let core = Core.spawn system
        let xmpp = spawn system "xmpp" (xmppActor core config.xmpp)
        let telegram = spawn system "telegram" (telegramActor core config.telegram)
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
