module Emulsion.Program

open System
open System.IO

open Akka.Actor
open Microsoft.Extensions.Configuration

open System.Threading
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

let private startMessageSystem (system: IMessageSystem) receiver =
    Async.StartChild <| async {
        do! Async.SwitchToNewThread()
        try
            system.Run receiver
        with
        | ex -> logError ex
    }

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
        let! cancellationToken = Async.CancellationToken
        let xmpp = Xmpp.Client(restartContext, cancellationToken, config.xmpp)
        let telegram = Telegram.Client(restartContext, cancellationToken, config.telegram)
        let factories = { xmppFactory = Xmpp.spawn xmpp
                          telegramFactory = Telegram.spawn telegram }
        printfn "Prepare Core..."
        let core = Core.spawn factories system "core"
        printfn "Starting message systems..."
        let! telegramSystem = startMessageSystem telegram (fun m -> core.Tell(TelegramMessage m))
        let! xmppSystem = startMessageSystem xmpp (fun m -> core.Tell(XmppMessage m))
        printfn "Ready. Wait for termination..."
        do! Async.AwaitTask system.WhenTerminated
        printfn "Waiting for terminating of message systems..."
        do! telegramSystem
        do! xmppSystem
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
