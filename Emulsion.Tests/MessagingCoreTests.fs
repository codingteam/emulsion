namespace Emulsion.Tests

open System
open System.Threading
open System.Threading.Tasks
open Emulsion
open Emulsion.Messaging
open Emulsion.Messaging.MessageSystem
open Emulsion.TestFramework
open JetBrains.Collections.Viewable
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

    let waitForSignal (lt: Lifetime) (signal: ISource<_>) =
        let tcs = lt.CreateTaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)
        signal.AdviseOnce(lt, fun() -> tcs.SetResult())
        tcs.Task

    let newMessageSystem (receivedMessages: ResizeArray<_>) = {
        new IMessageSystem with
            override this.PutMessage m = lock receivedMessages (fun() -> receivedMessages.Add m)
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
        let messages = ResizeArray()
        let archive = {
            new IMessageArchive with
                override this.Archive(message) =
                    lock messages (fun() -> messages.Add message)
                    async.Return()
        }

        let core = MessagingCore(lt, logger, Some archive)
        let awaitMessage = waitForSignal lt core.MessageProcessedSuccessfully
        core.Start(dummyMessageSystem, dummyMessageSystem)

        let message = IncomingMessage.TelegramMessage(testMessage)
        core.ReceiveMessage message
        do! awaitMessage

        Assert.Equal([|message|], messages)
    }

    [<Fact>]
    member _.``MessagingCore sends XMPP message to Telegram and vise-versa``(): Task = task {
        let telegramReceived = ResizeArray()
        let xmppReceived = ResizeArray()

        let xmpp = newMessageSystem xmppReceived
        let telegram = newMessageSystem telegramReceived

        use ld = new LifetimeDefinition()
        let lt = ld.Lifetime
        let core = MessagingCore(lt, logger, None)
        core.Start(telegram, xmpp)

        let sendMessageAndAssertReceival incomingMessage text (received: _ seq) = task {
            let awaitMessage = waitForSignal lt core.MessageProcessedSuccessfully
            let message = Authored {
                author = "cthulhu"
                text = text
            }

            let incoming = incomingMessage message
            core.ReceiveMessage incoming
            do! awaitMessage
            lock received (fun() ->
                Assert.Equal([|OutgoingMessage message|], received)
            )
        }

        do! sendMessageAndAssertReceival XmppMessage "text1" telegramReceived
        do! sendMessageAndAssertReceival TelegramMessage "text2" xmppReceived
    }

    [<Fact>]
    member _.``MessagingCore buffers the message received before start``(): Task = task {
        let telegramReceived = ResizeArray()
        let telegram = newMessageSystem telegramReceived

        use ld = new LifetimeDefinition()
        let lt = ld.Lifetime
        let core = MessagingCore(lt, logger, None)

        core.ReceiveMessage(XmppMessage testMessage)
        Assert.Empty(lock telegramReceived (fun() -> telegramReceived))

        core.Start(telegram, dummyMessageSystem)
        do! waitForSignal lt core.MessageProcessedSuccessfully

        let receivedMessage = Assert.Single(lock telegramReceived (fun() -> telegramReceived))
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
        let awaitMessage = waitForSignal lt core.MessageProcessedSuccessfully

        let awaitError = waitForSignal lt core.MessageProcessingError
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
