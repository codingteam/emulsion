module Emulsion.Logging

open System.IO

open Serilog
open Serilog.Core
open Serilog.Filters

open Emulsion.Settings
open Serilog.Formatting.Json

type EventCategory =
    Telegram | Xmpp

let private EventCategoryProperty = "EventCategory"

let loggerWithCategory (category: EventCategory) (logger: ILogger) =
    let enricher =
        { new ILogEventEnricher with
             member __.Enrich(logEvent, propertyFactory) =
                 logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(EventCategoryProperty, category)) }
    logger.ForContext enricher

let createRootLogger (settings: LogSettings) =
    let addFileLogger (category: EventCategory option) fileName (config: LoggerConfiguration) =
        let filePath = Path.Combine(settings.Directory, fileName)
        config.WriteTo.Logger(fun subConfig ->
            let filtered =
                match category with
                | Some c -> subConfig.Filter.ByIncludingOnly(Matching.WithProperty(EventCategoryProperty, c))
                | None -> subConfig.Filter.ByExcluding(Matching.WithProperty EventCategoryProperty)

            filtered.WriteTo.RollingFile(JsonFormatter(), filePath)
            |> ignore
        )

    let config =
        LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Console()
            |> addFileLogger (Some Telegram) "telegram.log"
            |> addFileLogger (Some Xmpp) "xmpp.log"
            |> addFileLogger None "system.log"
    config.CreateLogger()
