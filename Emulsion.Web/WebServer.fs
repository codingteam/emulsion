module Emulsion.Web.WebServer

open System.Threading.Tasks

open Emulsion.Database
open Emulsion.Settings
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Serilog

let run (logger: ILogger) (hostingSettings: HostingSettings) (databaseSettings: DatabaseSettings): Task =
    let builder = WebApplication.CreateBuilder(WebApplicationOptions())

    builder.Host.UseSerilog(logger)
    |> ignore

    builder.Services
        .AddSingleton(hostingSettings)
        .AddTransient<EmulsionDbContext>(fun _ -> new EmulsionDbContext(databaseSettings.ContextOptions))
        .AddControllers()
        .AddApplicationPart(typeof<ContentController>.Assembly)
    |> ignore

    let app = builder.Build()
    app.MapControllers() |> ignore
    app.RunAsync(hostingSettings.BindUri)
