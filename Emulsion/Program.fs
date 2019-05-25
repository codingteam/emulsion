module Emulsion.Program

open System.IO

open Akka.Actor
open Microsoft.Extensions.Configuration

open Emulsion.Actors
open Emulsion.Settings

let private getConfiguration directory fileName =
    let config =
        ConfigurationBuilder()
            .SetBasePath(directory)
            .AddJsonFile(fileName)
            .Build()
    Settings.read config

let private startApp config =
    async {
        printfn "Prepare system..."
        use system = ActorSystem.Create("emulsion")
        printfn "Prepare factories..."
        let xmpp = Xmpp.Client.sharpXmpp config.xmpp
        let telegram = Telegram.Client.funogram config.telegram
        let factories = { xmppFactory = Xmpp.spawn xmpp
                          telegramFactory = Telegram.spawn telegram }
        printfn "Prepare Core..."
        ignore <| Core.spawn factories system "core"
        printfn "Ready. Wait for termination..."
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
