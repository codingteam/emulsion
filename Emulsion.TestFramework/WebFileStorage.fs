namespace Emulsion.TestFramework

open System
open System.Net
open System.Net.Sockets

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Http
open Serilog

module private NetUtil =
    let findFreePort() =
        use socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
        socket.Bind(IPEndPoint(IPAddress.Loopback, 0))
        (socket.LocalEndPoint :?> IPEndPoint).Port

type WebFileStorage(logger: ILogger, data: Map<string, byte[]>) =
    let url = $"http://localhost:{NetUtil.findFreePort()}"

    let startWebApplication() =
        let builder = WebApplication.CreateBuilder()
        let app = builder.Build()
        app.MapGet("/{entry}", Func<_, _>(fun (entry: string) -> task {
            match Map.tryFind entry data with
            | Some bytes -> return Results.Bytes bytes
            | None -> return Results.NotFound()
        })) |> ignore
        app, app.RunAsync url

    let app, task = startWebApplication()

    member _.Link(entry: string): Uri =
        Uri $"{url}/{entry}"

    member _.Content(entry: string): byte[] =
        data[entry]

    interface IDisposable with
        member this.Dispose(): unit =
            Console.WriteLine "TEST LOGGING"
            Console.WriteLine("Stopping the test web server…")
            app.StopAsync().Wait()
            Console.WriteLine("Stopped the test web server, waiting for app.RunAsync() to finish…")
            task.Wait()
            Console.WriteLine("Stopped the test web server completely.")
