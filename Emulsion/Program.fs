module Emulsion.Program

open System
open System.IO

open Akka.Actor
open Microsoft.Extensions.Configuration

open Emulsion.Actors
open Emulsion.MessageSystem
open Emulsion.Settings

let private getConfiguration directory fileName =
    let config =
        ConfigurationBuilder()
            .SetBasePath(directory)
            .AddJsonFile(fileName)
            .Build()
    Settings.read config

let private logError = printfn "ERROR: %A"
let private logInfo = printfn "INFO : %s"

let private startApp config =
    async {
        printfn "Prepare system..."
        use system = ActorSystem.Create("emulsion")
        printfn "Prepare factories..."
        let restartContext = {
            cooldown = TimeSpan.FromSeconds(30.0) // TODO[F]: Customize through the config.
            logError = logError
            logMessage = logInfo
        }
        let xmpp = Xmpp.Client.sharpXmpp config.xmpp
        let telegram = Telegram.Client(restartContext, config.telegram)
        let factories = { xmppFactory = Xmpp.spawn xmpp
                          telegramFactory = fun factory _ name -> Telegram.spawn telegram factory name } // TODO[F]: Change the architecture here so we don't need to ignore the `core` parameter.
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
