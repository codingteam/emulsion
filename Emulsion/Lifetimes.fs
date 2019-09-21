module Emulsion.Lifetimes

open System.Threading.Tasks

open JetBrains.Lifetimes

let awaitTermination(lifetime: Lifetime): Async<unit> =
    let tcs = TaskCompletionSource()
    lifetime.OnTermination(fun () -> tcs.SetResult()) |> ignore
    Async.AwaitTask tcs.Task
