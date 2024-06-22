// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

module Emulsion.Logging

open System.IO

open JetBrains.Diagnostics
open Serilog
open Serilog.Core
open Serilog.Events
open Serilog.Filters

open Emulsion.Settings
open Serilog.Formatting.Json

type private EventCategory =
    Telegram | Xmpp

let private EventCategoryProperty = "EventCategory"

let private loggerWithCategory (category: EventCategory) (logger: ILogger) =
    let enricher =
        { new ILogEventEnricher with
             member _.Enrich(logEvent, propertyFactory) =
                 logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty(EventCategoryProperty, category)) }
    logger.ForContext enricher

let telegramLogger: ILogger -> ILogger = loggerWithCategory Telegram
let xmppLogger: ILogger -> ILogger = loggerWithCategory Xmpp

let createRootLogger (settings: LogSettings) =
    let addFileLogger (category: EventCategory option) fileName (config: LoggerConfiguration) =
        let filePath = Path.Combine(settings.Directory, fileName)
        config.WriteTo.Logger(fun subConfig ->
            let filtered =
                match category with
                | Some c ->
                    let scalar = c.ToString() // required because log event properties are actually converted to strings
                    subConfig.Filter.ByIncludingOnly(Matching.WithProperty(EventCategoryProperty, scalar))
                | None -> subConfig.Filter.ByExcluding(Matching.WithProperty EventCategoryProperty)

            filtered.WriteTo.RollingFile(JsonFormatter(), filePath)
            |> ignore
        )

    let config =
        LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            |> addFileLogger (Some Telegram) "telegram.log"
            |> addFileLogger (Some Xmpp) "xmpp.log"
            |> addFileLogger None "system.log"
    config.CreateLogger()

let private toSerilog(level: LoggingLevel): LogEventLevel voption =
    match level with
    | LoggingLevel.OFF -> ValueNone
    | LoggingLevel.FATAL -> ValueSome LogEventLevel.Fatal
    | LoggingLevel.ERROR -> ValueSome LogEventLevel.Error
    | LoggingLevel.WARN -> ValueSome LogEventLevel.Warning
    | LoggingLevel.INFO -> ValueSome LogEventLevel.Information
    | LoggingLevel.VERBOSE -> ValueSome LogEventLevel.Verbose
    | LoggingLevel.TRACE -> ValueSome LogEventLevel.Debug
    | _ -> ValueSome LogEventLevel.Error // convert any unknown ones to error

let attachToRdLogSystem(serilog: ILogger) =
    Log.UsingLogFactory({
        new ILogFactory with
            override this.GetLog(category) =
                let serilogLogger = serilog.ForContext(Constants.SourceContextPropertyName, category)
                {
                    new ILog with
                        override this.IsEnabled(level) =
                            toSerilog level
                            |> ValueOption.map serilogLogger.IsEnabled
                            |> ValueOption.defaultValue false
                        override this.Log(level, message, ``exception``) =
                            toSerilog level
                            |> ValueOption.iter(fun level ->
                                serilogLogger.Write(level, ``exception``, message)
                            )
                        override this.Category = category
                }
    })
