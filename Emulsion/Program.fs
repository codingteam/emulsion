// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

module Emulsion.Program

open System
open System.IO
open System.Security.Cryptography

open JetBrains.Lifetimes
open Microsoft.Extensions.Configuration
open Serilog

open Emulsion.ContentProxy
open Emulsion.Database
open Emulsion.Messaging.MessageSystem
open Emulsion.Settings
open Emulsion.Web
open Emulsion.Xmpp

let private getConfiguration directory (fileName: string) =
    let config =
        ConfigurationBuilder()
            .SetBasePath(directory)
            .AddJsonFile(fileName)
            .Build()
    read config

let private migrateDatabase (logger: ILogger) (settings: DatabaseSettings) = async {
    logger.Information("Migrating the database {DataSource}…", settings.DataSource)
    use context = new EmulsionDbContext(settings.ContextOptions)
    do! DataStorage.initializeDatabase context
    logger.Information "Database migration completed."
}

let private serviceContext logger = {
    RestartCooldown = TimeSpan.FromSeconds(30.0) // TODO[F]: Customize through the config.
    Logger = logger
}

let private startMessageSystem (logger: ILogger) (system: IMessageSystem) receiver =
    Async.StartChild <| async {
        do! Async.SwitchToNewThread()
        try
            system.RunSynchronously receiver
        with
        | ex -> logger.Error(ex, "Message system error in {System}", system)
    }

let private startApp config =
    async {
        let logger = Logging.createRootLogger config.Log
        use _ = Logging.attachToRdLogSystem logger
        try
            let xmppLogger = Logging.xmppLogger logger
            let telegramLogger = Logging.telegramLogger logger

            let! cancellationToken = Async.CancellationToken
            let xmpp = XmppMessageSystem(serviceContext xmppLogger, cancellationToken, config.Xmpp)
            let telegram = Telegram.Client(serviceContext telegramLogger,
                                           cancellationToken,
                                           config.Telegram,
                                           config.Database,
                                           config.Hosting)

            use sha256 = SHA256.Create()
            let fileCacheOption = config.FileCache |> Option.map(fun settings ->
                let httpClientFactory = SimpleHttpClientFactory()
                new FileCache(logger, settings, httpClientFactory, sha256)
            )

            try
                match config.Database with
                | Some dbSettings -> do! migrateDatabase logger dbSettings
                | None -> ()

                let webServerTask =
                    match config.Hosting, config.Database with
                    | Some hosting, Some database ->
                        logger.Information "Initializing the web server…"
                        Some <| WebServer.run logger hosting database config.MessageArchive telegram fileCacheOption
                    | _ -> None

                logger.Information "Core preparation…"
                let archive =
                    match config.Database, config.MessageArchive.IsEnabled with
                    | Some database, true -> Some(MessageArchive database :> IMessageArchive)
                    | _ -> None
                use lt = Lifetime.Define "app"
                let core = MessagingCore(lt.Lifetime, logger, archive)
                logger.Information "Message systems preparation…"
                let! telegramSystem = startMessageSystem logger telegram core.ReceiveMessage
                let! xmppSystem = startMessageSystem logger xmpp core.ReceiveMessage
                logger.Information "Starting the core…"
                core.Start(telegram, xmpp)
                logger.Information "System ready"

                logger.Information "Waiting for the systems to terminate…"
                do! Async.Ignore <| Async.Parallel(seq {
                    yield telegramSystem
                    yield xmppSystem

                    match webServerTask with
                    | Some task -> yield Async.AwaitTask task
                    | None -> ()
                })
            finally
                fileCacheOption |> Option.iter(fun x -> (x :> IDisposable).Dispose())
                logger.Information "Terminated successfully."
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
        let fullConfigPath = Path.GetFullPath configPath
        getConfiguration (Path.GetDirectoryName fullConfigPath) (Path.GetFileName configPath)
        |> startApp
        |> runApp
    | _ ->
        printfn $"Arguments: [config file name] ({defaultConfigFileName} by default)"
        0
