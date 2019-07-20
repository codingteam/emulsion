module Emulsion.Program

open System
open System.IO

open Akka.Actor
open Microsoft.Extensions.Configuration
open Serilog

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

let private startMessageSystem (logger: ILogger) (system: IMessageSystem) receiver =
    Async.StartChild <| async {
        do! Async.SwitchToNewThread()
        try
            system.Run receiver
        with
        | ex -> logger.Error(ex, "Message system error {System}", system)
    }

let private startApp config =
    async {
        let logger = Logging.createRootLogger config.Log
        try
            logger.Information "Actor system preparation…"
            use system = ActorSystem.Create("emulsion")
            logger.Information "Clients preparation…"
            let restartContext = {
                cooldown = TimeSpan.FromSeconds(30.0) // TODO[F]: Customize through the config.
                logError = logError
                logMessage = logInfo
            }
            let! cancellationToken = Async.CancellationToken
            let xmpp = Xmpp.Client(restartContext, cancellationToken, config.Xmpp)
            let telegram = Telegram.Client(restartContext, cancellationToken, config.Telegram)
            let factories = { xmppFactory = Xmpp.spawn xmpp
                              telegramFactory = Telegram.spawn telegram }
            logger.Information "Core preparation…"
            let core = Core.spawn factories system "core"
            logger.Information "Message systems preparation…"
            let! telegramSystem = startMessageSystem logger telegram core.Tell
            let! xmppSystem = startMessageSystem logger xmpp core.Tell
            logger.Information "System ready"

            logger.Information "Waiting for actor system termination…"
            do! Async.AwaitTask system.WhenTerminated
            logger.Information "Waiting for message systems termination…"
            do! telegramSystem
            do! xmppSystem
        with
        | error ->
            logger.Fatal(error, "General application failure")
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
