namespace Emulsion.Web

open System.Threading.Tasks

open Emulsion.Database.Entities
open Emulsion.Telegram
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging

open Emulsion.ContentProxy
open Emulsion.Database
open Emulsion.Settings

[<ApiController>]
[<Route("content")>]
type ContentController(logger: ILogger<ContentController>,
                       configuration: HostingSettings,
                       telegram: ITelegramClient,
                       context: EmulsionDbContext) =
    inherit ControllerBase()

    let decodeHashId hashId =
        try
            Some <| Proxy.decodeHashId configuration.HashIdSalt hashId
        with
            | ex ->
                logger.LogWarning(ex, "Error during hashId deserializing")
                None

    let produceRedirect contentId: Async<IActionResult option> = async {
        let! content = ContentStorage.getById context contentId
        match content with
        | Some content ->
            let! url = telegram.GetTemporaryFileLink content.FileId
            return Some <| RedirectResult(url.ToString())
        | None -> return None
    }

    [<HttpGet("{hashId}")>]
    member this.Get(hashId: string): Task<IActionResult> = task {
        match decodeHashId hashId with
        | None -> return this.BadRequest()
        | Some contentId ->
            match! produceRedirect contentId with
            | None -> return this.NotFound() :> IActionResult
            | Some redirect -> return redirect
    }
