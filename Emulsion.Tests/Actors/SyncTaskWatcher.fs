namespace Emulsion.Tests.Actors

open System
open System.Threading

open Akka.Actor
open Akka.TestKit
open Akka.TestKit.Xunit2
open Xunit

open Emulsion.Actors

type TestWatcher(parent : IActorRef) =
    inherit SyncTaskWatcher()
    member val PreStartThreadId = 0 with get, set
    member val RunInTaskThreadId = 0 with get, set

    override this.PreStart() =
        base.PreStart()
        this.PreStartThreadId <- Thread.CurrentThread.ManagedThreadId

    override this.RunInTask() =
        this.RunInTaskThreadId <- Thread.CurrentThread.ManagedThreadId

type ErrorTestWatcher(parent : IActorRef) =
    inherit TestWatcher(parent)
    override this.RunInTask() = failwith "Error"

type SyncTaskWatcherTests() =
    inherit TestKit()

    member this.createTestWatcher<'T when 'T :> ActorBase>() =
        let props = Props.Create<'T>(this.TestActor)
        TestActorRef<'T>(this.Sys, props)

    [<Fact>]
    member this.``SyncTaskWatcher should start a task in a separate thread``() =
        let threadId = Thread.CurrentThread.ManagedThreadId
        let watcher = this.createTestWatcher<TestWatcher>()
        let actor = watcher.UnderlyingActor
        this.Sys.Stop watcher
        this.ExpectNoMsg()

        Assert.NotEqual(actor.PreStartThreadId, actor.RunInTaskThreadId)

    [<Fact>]
    member this.``SyncTaskWatcher dies when the thread throws an error``() =
        let errored = this.createTestWatcher<ErrorTestWatcher>()
        this.Watch errored |> ignore
        this.ExpectTerminated errored |> ignore
