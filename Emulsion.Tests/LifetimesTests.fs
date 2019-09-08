module Emulsion.Tests.LifetimesTests

open JetBrains.Lifetimes

open Xunit

open System.Threading.Tasks
open Emulsion.Lifetimes

[<Fact>]
let ``nestedTaskCompletionSource getting cancelled after parent lifetime termination``(): unit =
    use ld = Lifetime.Define()
    let tcs = nestedTaskCompletionSource ld.Lifetime
    let task = tcs.Task
    Assert.False task.IsCompleted
    ld.Terminate()
    Assert.Throws<TaskCanceledException>(fun () -> task.GetAwaiter().GetResult() |> ignore) |> ignore

[<Fact>]
let ``awaitTermination completes after the parent lifetime is terminated``(): unit =
    use ld = Lifetime.Define()
    let task = Async.StartAsTask <| awaitTermination ld.Lifetime
    Assert.False task.IsCompleted
    ld.Terminate()
    task.GetAwaiter().GetResult() |> ignore
