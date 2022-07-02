namespace Emulsion.Web

open System.Threading.Tasks

open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging

open Emulsion.ContentProxy
open Emulsion.Database
open Emulsion.Settings

[<ApiController>]
[<Route("content")>]
type ContentController(logger: ILogger<ContentController>,
                       configuration: HostingSettings,
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
        return
            content
            |> Option.map(fun c ->
                let url = $"https://t.me/{c.ChatUserName}/{string c.MessageId}"
                RedirectResult url
            )
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
