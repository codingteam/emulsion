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
open Emulsion.Tests.TestUtils
open Emulsion.Tests.TestUtils.Waiter

let private testContext = {
    Send = fun _ -> async { return () }
    Logger = Logger.None
    RestartCooldown = TimeSpan.Zero
}

[<Fact>]
let ``Message sender sends the messages sequentially``() =
    use cts = new CancellationTokenSource()
    let buffer = LockedBuffer()
    let context = {
        testContext with
            Send = fun m -> async {
                buffer.Add m
            }
    }
    let sender = MessageSender.startActivity(context, cts.Token)
    MessageSender.setReadyToAcceptMessages sender true

    let messagesSent = [| 1..100 |] |> Array.map (fun i ->
        OutgoingMessage {
            author = "author"
            text = string i
        }
    )
    messagesSent |> Array.iter(MessageSender.send sender)

    waitForItemCount buffer messagesSent.Length defaultTimeout
    |> Assert.True

    Assert.Equal(messagesSent, buffer.All())

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

        SpinWait.SpinUntil((fun () -> Seq.length(getErrors()) > 0), shortTimeout) |> ignore
        Assert.Empty <| getErrors()
    )

let ``Message sender does nothing when the system is not ready to process the messages``() =
    use cts = new CancellationTokenSource()
    let buffer = LockedBuffer()
    let context = {
        testContext with
            Send = fun m -> async {
                buffer.Add m
            }
    }
    let sender = MessageSender.startActivity(context, cts.Token)
    let msg = OutgoingMessage { author = "author"; text = "xx" }

    MessageSender.setReadyToAcceptMessages sender true
    MessageSender.send sender msg
    waitForItemCount buffer 1 defaultTimeout |> Assert.True

    MessageSender.setReadyToAcceptMessages sender false
    MessageSender.send sender msg
    waitForItemCount buffer 2 shortTimeout |> Assert.False
