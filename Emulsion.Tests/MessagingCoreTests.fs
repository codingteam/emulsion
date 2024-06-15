namespace Emulsion.Tests

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
        let awaitMessage = waitForSignal lt core.MessageProcessed
        core.Start(dummyMessageSystem, dummyMessageSystem)

        let message = IncomingMessage.TelegramMessage(Authored {
            author = "cthulhu"
            text = "fhtagn"
        })
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
            let awaitMessage = waitForSignal lt core.MessageProcessed
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

        let message = Authored {
            author = "cthulhu"
            text = "fhtagn"
        }
        core.ReceiveMessage(XmppMessage message)
        Assert.Empty(lock telegramReceived (fun() -> telegramReceived))

        core.Start(telegram, dummyMessageSystem)
        do! waitForSignal lt core.MessageProcessed

        let receivedMessage = Assert.Single(lock telegramReceived (fun() -> telegramReceived))
        Assert.Equal(OutgoingMessage message, receivedMessage)
    }
