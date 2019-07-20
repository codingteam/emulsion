module Emulsion.Tests.MessageSenderTests

open System
open System.Threading

open Xunit
open Emulsion
open Emulsion.MessageSender

let private testContext = {
    Send = fun _ -> async { return () }
    LogError = ignore
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
    let errors = ResizeArray()
    let context = {
        testContext with
            Send = fun _ -> failwith "Should not be called"
            LogError = fun e -> lock errors (fun () -> errors.Add e)
    }
    let sender = MessageSender.startActivity(context, cts.Token)
    cts.Cancel()

    let msg = OutgoingMessage { author = "author"; text = "xx" }
    MessageSender.send sender msg

    SpinWait.SpinUntil((fun () -> errors.Count > 0), TimeSpan.FromMilliseconds 100.0) |> ignore
    Assert.Empty errors
