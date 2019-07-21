module Emulsion.Tests.MessageSenderTests

open System
open System.Threading

open Serilog
open Serilog.Core
open Serilog.Events
open Serilog.Sinks.TestCorrelator
open Xunit

open Emulsion
open Emulsion.MessageSender

let private testContext = {
    Send = fun _ -> async { return () }
    Logger = Logger.None
    RestartCooldown = TimeSpan.Zero
}

[<Fact>]
let ``Message sender sends the messages sequentially``() =
    use cts = new CancellationTokenSource()
    let messagesReceived = ResizeArray()
    let context = {
        testContext with
            Send = fun m -> async {
                lock messagesReceived (fun () ->
                    messagesReceived.Add m
                )
            }
    }
    let sender = MessageSender.startActivity(context, cts.Token)

    let messagesSent = [| 1..100 |] |> Array.map (fun i ->
        OutgoingMessage {
            author = "author"
            text = string i
        }
    )
    messagesSent |> Array.iter(MessageSender.send sender)

    SpinWait.SpinUntil((fun () -> messagesReceived.Count = messagesSent.Length), TimeSpan.FromSeconds 30.0)
    |> Assert.True

    Assert.Equal(messagesSent, messagesReceived)

[<Fact>]
let ``Message sender should be cancellable``() =
    use cts = new CancellationTokenSource()
    using (TestCorrelator.CreateContext()) (fun _ ->
        let context = {
            testContext with
                Send = fun _ -> failwith "Should not be called"
                Logger = LoggerConfiguration().WriteTo.TestCorrelator().CreateLogger()
        }
        let sender = MessageSender.startActivity(context, cts.Token)
        cts.Cancel()

        let msg = OutgoingMessage { author = "author"; text = "xx" }
        MessageSender.send sender msg

        let getErrors() =
            TestCorrelator.GetLogEventsFromCurrentContext()
            |> Seq.filter (fun event -> event.Level = LogEventLevel.Error)

        SpinWait.SpinUntil((fun () -> Seq.length(getErrors()) > 0),
                           TimeSpan.FromMilliseconds 1000.0) |> ignore
        Assert.Empty <| getErrors()
    )
