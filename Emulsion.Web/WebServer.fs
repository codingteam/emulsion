module Emulsion.Web.WebServer

open System
open System.Threading.Tasks

open Emulsion.Web.Controllers
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection

let run(baseUri: Uri): Task =
    // TODO: Pass baseUri
    let builder = WebApplication.CreateBuilder()
    builder.Services
        .AddControllers()
        .AddApplicationPart(typeof<WeatherForecastController>.Assembly)
    |> ignore

    let app = builder.Build()
    app.MapControllers() |> ignore
    app.RunAsync()
