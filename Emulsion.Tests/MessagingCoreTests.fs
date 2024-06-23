// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

namespace Emulsion.Tests

open System
open System.Threading
open System.Threading.Channels
open System.Threading.Tasks
open Emulsion
open Emulsion.Messaging
open Emulsion.Messaging.MessageSystem
open Emulsion.TestFramework
open JetBrains.Lifetimes
open Xunit
open Xunit.Abstractions

type MessagingCoreTests(output: ITestOutputHelper) =

    let logger = Logging.xunitLogger output
    let dummyMessageSystem = {
        new IMessageSystem with
            override this.PutMessage _ = ()
            override this.RunSynchronously _ = ()
    }

    let waitTimeout = TimeSpan.FromSeconds 10.0
    let waitSuccessfulProcessing lt (core: MessagingCore) =
        Signals.WaitWithTimeout lt core.MessageProcessedSuccessfully waitTimeout "message processed successfully"
    let waitProcessingError lt (core: MessagingCore) =
        Signals.WaitWithTimeout lt core.MessageProcessingError waitTimeout "message processed with error"

    let newMessageSystem (receivedMessages: Channel<_>) = {
        new IMessageSystem with
            override this.PutMessage m =
                let result = receivedMessages.Writer.TryWrite m
                Assert.True(result, "Channel should accept an incoming message")
            override this.RunSynchronously _ = ()
    }

    let testMessage = Authored {
        author = "cthulhu"
        text = "fhtagn"
    }

    [<Fact>]
    member _.``MessagingCore calls archive if it's present``(): Task = task {
        use ld = new LifetimeDefinition()
        let lt = ld.Lifetime
        let messages = Channel.CreateUnbounded()
        let archive = {
            new IMessageArchive with
                override this.Archive(message) =
                    messages.Writer.WriteAsync(message).AsTask()
                    |> Async.AwaitTask
        }

        let core = MessagingCore(lt, logger, Some archive)
        core.Start(dummyMessageSystem, dummyMessageSystem)

        let expected = IncomingMessage.TelegramMessage(testMessage)
        core.ReceiveMessage expected
        let! actual = messages.Reader.ReadAsync()

        Assert.Equal(expected, actual)
    }

    [<Fact>]
    member _.``MessagingCore sends XMPP message to Telegram and vise-versa``(): Task = task {
        let telegramReceived = Channel.CreateUnbounded()
        let xmppReceived = Channel.CreateUnbounded()

        let xmpp = newMessageSystem xmppReceived
        let telegram = newMessageSystem telegramReceived

        use ld = new LifetimeDefinition(Id = "Test core lifetime")
        let lt = ld.Lifetime
        let core = MessagingCore(lt, logger, None)
        core.Start(telegram, xmpp)

        let sendMessageAndAssertReceival incomingMessage text (received: Channel<_>) = task {
            let message = Authored {
                author = "cthulhu"
                text = text
            }

            let incoming = incomingMessage message
            core.ReceiveMessage incoming
            let! outgoing = received.Reader.ReadAsync()
            Assert.Equal(OutgoingMessage message, outgoing)
        }

        do! sendMessageAndAssertReceival XmppMessage "text1" telegramReceived
        do! sendMessageAndAssertReceival TelegramMessage "text2" xmppReceived
    }

    [<Fact>]
    member _.``MessagingCore buffers the message received before start``(): Task = task {
        let telegramReceived = Channel.CreateUnbounded()
        let telegram = newMessageSystem telegramReceived

        use ld = new LifetimeDefinition()
        let lt = ld.Lifetime
        let core = MessagingCore(lt, logger, None)

        core.ReceiveMessage(XmppMessage testMessage)
        let hasMessages, _ = telegramReceived.Reader.TryPeek()
        Assert.False(hasMessages, "No message is expected to be available.")

        core.Start(telegram, dummyMessageSystem)
        let! receivedMessage = telegramReceived.Reader.ReadAsync()
        Assert.Equal(OutgoingMessage testMessage, receivedMessage)
    }

    [<Fact>]
    member _.``MessagingCore terminates its processing``(): Task = task {
        use ld = new LifetimeDefinition()
        let lt = ld.Lifetime
        let core = MessagingCore(lt, logger, None)

        let message = Authored {
            author = "cthulhu"
            text = "fhtagn"
        }
        for _ in 1..100 do
            core.ReceiveMessage(XmppMessage message)

        core.Start(dummyMessageSystem, dummyMessageSystem)
        ld.Terminate()

        Assert.True(
            SpinWait.SpinUntil((fun() -> core.ProcessingTask.Value.IsCompleted), TimeSpan.FromSeconds 1.0),
            "Task should be completed in time"
        )
    }

    [<Fact>]
    member _.``MessagingCore should log an error if receiving a message after termination``(): Task = task {
        use ld = new LifetimeDefinition()
        let lt = ld.Lifetime
        let core = MessagingCore(lt, logger, None)
        core.Start(dummyMessageSystem, dummyMessageSystem)
        ld.Terminate()

        Lifetime.Using(fun lt ->
            let mutable signaled = false
            core.MessageCannotBeReceived.Advise(lt, fun() -> signaled <- true)
            core.ReceiveMessage(TelegramMessage testMessage)
            Assert.True(signaled, "Error on message after termination should be reported.")
        )
    }

    [<Fact>]
    member _.``MessagingCore should log an error during processing``(): Task = task {
        use ld = new LifetimeDefinition()
        let lt = ld.Lifetime
        let core = MessagingCore(lt, logger, None)

        let mutable shouldThrow = true
        let received = ResizeArray()
        let throwingSystem = {
            new IMessageSystem with
                member this.PutMessage m =
                    if Volatile.Read(&shouldThrow)
                    then failwith "Error."
                    else lock received (fun() -> received.Add m)
                member this.RunSynchronously _ = ()
        }

        core.Start(telegram = throwingSystem, xmpp = dummyMessageSystem)
        let awaitMessage = waitSuccessfulProcessing lt core

        let awaitError = waitProcessingError lt core
        core.ReceiveMessage(XmppMessage testMessage)
        do! awaitError // error signalled correctly

        Volatile.Write(&shouldThrow, false)
        do! Lifetime.UsingAsync(fun lt -> task {
            let mutable signaled = false
            core.MessageProcessingError.Advise(lt, fun() -> Volatile.Write(&signaled, true))
            core.ReceiveMessage(XmppMessage testMessage)
            do! awaitMessage
            Assert.False(Volatile.Read(&signaled), "There should be no error.")
        })
    }
