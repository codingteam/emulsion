namespace Emulsion.Tests.MessageSystemTests

open System
open System.Threading

open Xunit
open Xunit.Abstractions

open System.Threading.Tasks
open Emulsion
open Emulsion.MessageSystem
open Emulsion.Tests.TestUtils
open Emulsion.Tests.TestUtils.Waiter

type MessageSystemBaseTests(testLogger: ITestOutputHelper) =
    let logger = Logging.xunitLogger testLogger

    [<Fact>]
    member __.``Message system should send the messages after being started``() =
        let context = { RestartCooldown = TimeSpan.Zero; Logger = logger }
        let buffer = LockedBuffer()
        use cts = new CancellationTokenSource()
        let messageSystem =
            { new MessageSystemBase(context, cts.Token) with
                member __.RunUntilError _ =
                    Async.Sleep Int32.MaxValue
                member __.Send m = async {
                    buffer.Add m
                }
            }
        let msg = OutgoingMessage { author = "author"; text = "text" }
        MessageSystem.putMessage messageSystem msg

        let messageReceiver = ignore
        let runningSystem = Task.Run(fun () -> (messageSystem :> IMessageSystem).Run messageReceiver)

        waitForItemCount buffer 1 defaultTimeout |> Assert.True
        Assert.Equal<OutgoingMessage>(Seq.singleton msg, buffer.All())

        cts.Cancel()
        Assert.Throws<OperationCanceledException>(fun () -> runningSystem.GetAwaiter().GetResult())
