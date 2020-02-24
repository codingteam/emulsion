namespace Emulsion.Tests.MessageSystemTests

open System
open System.Threading
open System.Threading.Tasks

open Xunit
open Xunit.Abstractions

open Emulsion
open Emulsion.MessageSystem
open Emulsion.Tests.TestUtils
open Emulsion.Tests.TestUtils.Waiter

type MessageSystemBaseTests(testLogger: ITestOutputHelper) =
    let logger = Logging.xunitLogger testLogger

    let msg = OutgoingMessage (Authored { author = "author"; text = "text" })

    [<Fact>]
    member _.``Message system should not send any messages before being started``(): unit =
        let context = { RestartCooldown = TimeSpan.Zero; Logger = logger }
        let buffer = LockedBuffer()
        use cts = new CancellationTokenSource()
        let mutable enteredRunLoop = false
        let tcs = TaskCompletionSource<unit>()
        let messageSystem : IMessageSystem =
            upcast { new MessageSystemBase(context, cts.Token) with
                member _.RunUntilError _ =
                    async {
                        do! Async.AwaitTask tcs.Task
                        return async {
                            Volatile.Write(&enteredRunLoop, true)
                            do! Async.Sleep Int32.MaxValue
                        }
                    }
                member _.Send m = async {
                    buffer.Add m
                }
            }
        // Start the system but don't let it to start the internal loop yet:
        MessageSystem.putMessage messageSystem msg

        // No messages sent:
        waitForItemCount buffer 1 shortTimeout |> Assert.False
        Assert.Equal(false, Volatile.Read &enteredRunLoop)

        let task = Async.StartAsTask(async { messageSystem.RunSynchronously ignore }, cancellationToken = cts.Token)

        // Still no messages sent (because the task hasn't really been started yet):
        waitForItemCount buffer 1 shortTimeout |> Assert.False
        Assert.Equal(false, Volatile.Read &enteredRunLoop)

        // Now allow the task to start:
        tcs.SetResult()

        // Now the system should have entered the run loop and the message should be sent:
        waitForItemCount buffer 1 defaultTimeout |> Assert.True
        SpinWait.SpinUntil((fun () -> Volatile.Read &enteredRunLoop), shortTimeout) |> Assert.True
        Assert.Equal<OutgoingMessage>(Seq.singleton msg, buffer.All())

        // Terminate the system:
        cts.Cancel()
        Assert.Throws<OperationCanceledException>(fun() -> task.GetAwaiter().GetResult()) |> ignore

    [<Fact>]
    member _.``Message system should send the messages after being started``() =
        let context = { RestartCooldown = TimeSpan.Zero; Logger = logger }
        let buffer = LockedBuffer()
        use cts = new CancellationTokenSource()
        let messageSystem =
            { new MessageSystemBase(context, cts.Token) with
                member _.RunUntilError _ =
                    async { return Async.Sleep Int32.MaxValue }
                member _.Send m = async {
                    buffer.Add m
                }
            }
        MessageSystem.putMessage messageSystem msg

        let messageReceiver = ignore
        let runningSystem = Task.Run(fun () -> (messageSystem :> IMessageSystem).RunSynchronously messageReceiver)

        waitForItemCount buffer 1 defaultTimeout |> Assert.True
        Assert.Equal<OutgoingMessage>(Seq.singleton msg, buffer.All())

        cts.Cancel()
        Assert.Throws<OperationCanceledException>(fun () -> runningSystem.GetAwaiter().GetResult())

