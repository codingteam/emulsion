namespace Emulsion.Tests

open System
open System.Threading

open Serilog
open Serilog.Events
open Serilog.Sinks.TestCorrelator
open Xunit
open Xunit.Abstractions

open Emulsion
open Emulsion.MessageSender
open Emulsion.Tests.TestUtils
open Emulsion.Tests.TestUtils.Waiter

type MessageSenderTests(testOutput: ITestOutputHelper) =
    let testContext = {
        Send = fun _ -> async { return () }
        Logger = Logging.xunitLogger testOutput
        RestartCooldown = TimeSpan.Zero
    }

    let createSender ctx token =
        new MailboxProcessor<_>(MessageSender.receiver ctx, token)

    let createBufferedContext() =
        let buffer = LockedBuffer()
        let context = {
            testContext with
                Send = fun m -> async {
                    buffer.Add m
                }
        }
        buffer, context

    [<Fact>]
    member __.``Message sender sends the messages sequentially``(): unit =
        use cts = new CancellationTokenSource()
        let buffer, context = createBufferedContext()
        let sender = MessageSender.startActivity(context, cts.Token)
        MessageSender.setReadyToAcceptMessages sender true

        let messagesSent = [| 1..100 |] |> Array.map (fun i ->
            OutgoingMessage (Authored {
                author = "author"
                text = string i
            })
        )
        messagesSent |> Array.iter(MessageSender.send sender)

        waitForItemCount buffer messagesSent.Length defaultTimeout
        |> Assert.True

        Assert.Equal(messagesSent, buffer.All())

    [<Fact>]
    member __.``Message sender should be cancellable``(): unit =
        use cts = new CancellationTokenSource()
        using (TestCorrelator.CreateContext()) (fun _ ->
            let context = {
                testContext with
                    Send = fun _ -> failwith "Should not be called"
                    Logger = LoggerConfiguration().WriteTo.TestCorrelator().CreateLogger()
            }
            let sender = MessageSender.startActivity(context, cts.Token)
            cts.Cancel()

            let msg = OutgoingMessage (Authored { author = "author"; text = "xx" })
            MessageSender.send sender msg

            let getErrors() =
                TestCorrelator.GetLogEventsFromCurrentContext()
                |> Seq.filter (fun event -> event.Level = LogEventLevel.Error)

            SpinWait.SpinUntil((fun () -> Seq.length(getErrors()) > 0), shortTimeout) |> ignore
            Assert.Empty <| getErrors()
        )

    [<Fact>]
    member __.``Message sender does nothing when the system is not ready to process the messages``(): unit =
        use cts = new CancellationTokenSource()
        let buffer, context = createBufferedContext()
        let sender = MessageSender.startActivity(context, cts.Token)
        let msg = OutgoingMessage (Authored { author = "author"; text = "xx" })

        MessageSender.setReadyToAcceptMessages sender true
        MessageSender.send sender msg
        waitForItemCount buffer 1 defaultTimeout |> Assert.True

        MessageSender.setReadyToAcceptMessages sender false
        MessageSender.send sender msg
        waitForItemCount buffer 2 shortTimeout |> Assert.False

    [<Fact>]
    member __.``Message sender should empty the queue before blocking on further messages``(): unit =
        use cts = new CancellationTokenSource()
        let buffer, context = createBufferedContext()
        let sender = MessageSender.startActivity(context, cts.Token)
        MessageSender.setReadyToAcceptMessages sender false
        MessageSender.send sender (OutgoingMessage (Authored { author = "author"; text = "1" }))
        MessageSender.send sender (OutgoingMessage (Authored { author = "author"; text = "2" }))
        MessageSender.setReadyToAcceptMessages sender true
        waitForItemCount buffer 2 defaultTimeout |> Assert.True

    [<Fact>]
    member __.``Message sender should prioritize the SetReceiveStatus msg over flushing the queue``(): unit =
        use cts = new CancellationTokenSource()
        let buffer = LockedBuffer()
        let mutable sender = Unchecked.defaultof<_>
        let context = {
            testContext with
                Send = fun m -> async {
                    // Let's send the setReadyToAcceptMessages immediately before sending any message
                    MessageSender.setReadyToAcceptMessages sender false
                    buffer.Add m
                }
        }
        sender <- MessageSender.startActivity(context, cts.Token)

        // This will send a message and block the second one:
        MessageSender.setReadyToAcceptMessages sender true
        MessageSender.send sender (OutgoingMessage (Authored { author = "author"; text = "1" }))
        waitForItemCount buffer 1 defaultTimeout |> Assert.True

        MessageSender.send sender (OutgoingMessage (Authored { author = "author"; text = "2" }))
        waitForItemCount buffer 2 shortTimeout |> Assert.False

    [<Fact>]
    member __.``Message sender should process the queue first before sending any messages``(): unit =
        use cts = new CancellationTokenSource()
        let buffer, context = createBufferedContext()
        let sender = createSender context cts.Token

        // First, create the message queue:
        MessageSender.setReadyToAcceptMessages sender true
        MessageSender.send sender (OutgoingMessage (Authored { author = "author"; text = "1" }))
        MessageSender.send sender (OutgoingMessage (Authored { author = "author"; text = "2" }))
        MessageSender.send sender (OutgoingMessage (Authored { author = "author"; text = "3" }))
        MessageSender.setReadyToAcceptMessages sender false

        // Now start the processor and check that the full queue was processed before sending any messages:
        sender.Start()
        waitForItemCountCond buffer (fun c -> c > 0) shortTimeout |> Assert.False
