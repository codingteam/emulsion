module Emulsion.Web.WebServer

open System
open System.IO
open System.Threading.Tasks

open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.FileProviders
open Serilog

open Emulsion.ContentProxy
open Emulsion.Database
open Emulsion.Settings
open Emulsion.Telegram

let run (logger: ILogger)
        (hostingSettings: HostingSettings)
        (databaseSettings: DatabaseSettings)
        (messageArchiveSettings: MessageArchiveSettings)
        (telegram: ITelegramClient)
        (fileCache: FileCache option)
        : Task =
    let builder = WebApplication.CreateBuilder(WebApplicationOptions())
    if messageArchiveSettings.IsEnabled then
        builder.Environment.WebRootPath <- Path.Combine(Path.GetDirectoryName Environment.ProcessPath, "wwwroot")
        builder.Environment.WebRootFileProvider <- new PhysicalFileProvider(builder.Environment.WebRootPath)

    builder.Host.UseSerilog(logger)

    |> ignore

    builder.Services
        .AddSingleton(hostingSettings)
        .AddSingleton(telegram)
        .AddSingleton<Func<FileCache option>>(Func<_>(fun () -> fileCache))
        .AddTransient<EmulsionDbContext>(fun _ -> new EmulsionDbContext(databaseSettings.ContextOptions))
        .AddControllers()
        .AddApplicationPart(typeof<ContentController>.Assembly)
    |> ignore

    let app = builder.Build()
    app.MapControllers() |> ignore
    if messageArchiveSettings.IsEnabled then
        app.UseStaticFiles()
        |> ignore
    app.RunAsync(hostingSettings.BindUri)
