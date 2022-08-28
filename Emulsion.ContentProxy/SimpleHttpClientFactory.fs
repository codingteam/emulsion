namespace Emulsion.ContentProxy

open System.Net.Http

type SimpleHttpClientFactory() =
    interface IHttpClientFactory with
        member this.CreateClient _ = new HttpClient()
