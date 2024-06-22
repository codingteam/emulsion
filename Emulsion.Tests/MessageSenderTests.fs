// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

namespace Emulsion.Tests

open System
open System.Threading

open Serilog
open Serilog.Events
open Serilog.Sinks.TestCorrelator
open Xunit
open Xunit.Abstractions

open Emulsion.Messaging
open Emulsion.Messaging.MessageSender
open Emulsion.TestFramework
open Emulsion.TestFramework.Waiter

type MessageSenderTests(testOutput: ITestOutputHelper) =
    let testContext = {
        Send = fun _ -> async { return () }
        Logger = Logging.xunitLogger testOutput
        RestartCooldown = TimeSpan.Zero
    }

    let createSender ctx token =
        new MailboxProcessor<_>(receiver ctx, token)

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
    member _.``Message sender sends the messages sequentially``(): unit =
        use cts = new CancellationTokenSource()
        let buffer, context = createBufferedContext()
        let sender = startActivity(context, cts.Token)
        setReadyToAcceptMessages sender true

        let messagesSent = [| 1..100 |] |> Array.map (fun i ->
            OutgoingMessage (Authored {
                author = "author"
                text = string i
            })
        )
        messagesSent |> Array.iter(send sender)

        waitForItemCount buffer messagesSent.Length defaultTimeout
        |> Assert.True

        Assert.Equal(messagesSent, buffer.All())

    [<Fact>]
    member _.``Message sender should be cancellable``(): unit =
        use cts = new CancellationTokenSource()
        using (TestCorrelator.CreateContext()) (fun _ ->
            let context = {
                testContext with
                    Send = fun _ -> failwith "Should not be called"
                    Logger = LoggerConfiguration().WriteTo.TestCorrelator().CreateLogger()
            }
            let sender = startActivity(context, cts.Token)
            cts.Cancel()

            let msg = OutgoingMessage (Authored { author = "author"; text = "xx" })
            send sender msg

            let getErrors() =
                TestCorrelator.GetLogEventsFromCurrentContext()
                |> Seq.filter (fun event -> event.Level = LogEventLevel.Error)

            SpinWait.SpinUntil((fun () -> Seq.length(getErrors()) > 0), shortTimeout) |> ignore
            Assert.Empty(getErrors())
        )

    [<Fact>]
    member _.``Message sender does nothing when the system is not ready to process the messages``(): unit =
        use cts = new CancellationTokenSource()
        let buffer, context = createBufferedContext()
        let sender = startActivity(context, cts.Token)
        let msg = OutgoingMessage (Authored { author = "author"; text = "xx" })

        setReadyToAcceptMessages sender true
        send sender msg
        waitForItemCount buffer 1 defaultTimeout |> Assert.True

        setReadyToAcceptMessages sender false
        send sender msg
        waitForItemCount buffer 2 shortTimeout |> Assert.False

    [<Fact>]
    member _.``Message sender should empty the queue before blocking on further messages``(): unit =
        use cts = new CancellationTokenSource()
        let buffer, context = createBufferedContext()
        let sender = startActivity(context, cts.Token)
        setReadyToAcceptMessages sender false
        send sender (OutgoingMessage (Authored { author = "author"; text = "1" }))
        send sender (OutgoingMessage (Authored { author = "author"; text = "2" }))
        setReadyToAcceptMessages sender true
        waitForItemCount buffer 2 defaultTimeout |> Assert.True

    [<Fact>]
    member _.``Message sender should prioritize the SetReceiveStatus msg over flushing the queue``(): unit =
        use cts = new CancellationTokenSource()
        let buffer = LockedBuffer()
        let mutable sender = Unchecked.defaultof<_>
        let context = {
            testContext with
                Send = fun m -> async {
                    // Let's send the setReadyToAcceptMessages immediately before sending any message
                    setReadyToAcceptMessages sender false
                    buffer.Add m
                }
        }
        sender <- startActivity(context, cts.Token)

        // This will send a message and block the second one:
        setReadyToAcceptMessages sender true
        send sender (OutgoingMessage (Authored { author = "author"; text = "1" }))
        waitForItemCount buffer 1 defaultTimeout |> Assert.True

        send sender (OutgoingMessage (Authored { author = "author"; text = "2" }))
        waitForItemCount buffer 2 shortTimeout |> Assert.False

    [<Fact>]
    member _.``Message sender should process the queue first before sending any messages``(): unit =
        use cts = new CancellationTokenSource()
        let buffer, context = createBufferedContext()
        let sender = createSender context cts.Token

        // First, create the message queue:
        setReadyToAcceptMessages sender true
        send sender (OutgoingMessage (Authored { author = "author"; text = "1" }))
        send sender (OutgoingMessage (Authored { author = "author"; text = "2" }))
        send sender (OutgoingMessage (Authored { author = "author"; text = "3" }))
        setReadyToAcceptMessages sender false

        // Now start the processor and check that the full queue was processed before sending any messages:
        sender.Start()
        waitForItemCountCond buffer (fun c -> c > 0) shortTimeout |> Assert.False
