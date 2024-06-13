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
        let tcs = lt.CreateTaskCompletionSource()
        signal.AdviseOnce(lt, fun() -> tcs.SetResult())
        tcs.Task

    [<Fact>]
    member _.``MessagingCore calls archive if it's present``(): Task = task {
        use ld = new LifetimeDefinition()
        let lt = ld.Lifetime
        let messages = ResizeArray()
        let archive = {
            new IMessageArchive with
                override this.Archive(message) =
                    messages.Add message
                    async.Return()
        }

        let core = MessagingCore(lt, logger, Some archive)
        let awaitMessage = waitForSignal lt core.MessageProcessed
        core.Start(dummyMessageSystem, dummyMessageSystem)

        let message = IncomingMessage.TelegramMessage(Authored{
            author = "cthulhu"
            text = "fhtagn"
        })
        core.ReceiveMessage message
        do! awaitMessage

        Assert.Equal([|message|], messages)
    }
