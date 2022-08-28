module Emulsion.Web.WebServer

open System.Threading.Tasks

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Serilog

open Emulsion.ContentProxy
open Emulsion.Database
open Emulsion.Settings
open Emulsion.Telegram

let run (logger: ILogger)
        (hostingSettings: HostingSettings)
        (databaseSettings: DatabaseSettings)
        (telegram: ITelegramClient)
        (fileCache: FileCache option)
        : Task =
    let builder = WebApplication.CreateBuilder(WebApplicationOptions())

    builder.Host.UseSerilog(logger)
    |> ignore

    builder.Services
        .AddSingleton(hostingSettings)
        .AddSingleton(telegram)
        .AddSingleton(fileCache)
        .AddTransient<EmulsionDbContext>(fun _ -> new EmulsionDbContext(databaseSettings.ContextOptions))
        .AddControllers()
        .AddApplicationPart(typeof<ContentController>.Assembly)
    |> ignore

    let app = builder.Build()
    app.MapControllers() |> ignore
    app.RunAsync(hostingSettings.BindUri)
