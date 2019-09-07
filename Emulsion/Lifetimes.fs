module Emulsion.Lifetimes

open System.Threading.Tasks

open JetBrains.Lifetimes

/// Creates a task completion source that will be canceled if lifetime terminates before it is completed successfully.
let nestedTaskCompletionSource<'T>(lifetime: Lifetime): TaskCompletionSource<'T> =
    let tcs = new TaskCompletionSource<'T>()

    // Register a cancellation action, and remove the action when the task is completed (to not store the unnecessary
    // action after we already know it won't cancel the task).
    let cancellationToken = lifetime.ToCancellationToken()
    let action = cancellationToken.Register(fun () -> tcs.TrySetCanceled() |> ignore)
    tcs.Task.ContinueWith(fun (t: Task<'T>) -> action.Dispose()) |> ignore

    tcs

let awaitTermination(lifetime: Lifetime): Async<unit> =
    let tcs = TaskCompletionSource()
    lifetime.OnTermination(fun () -> tcs.SetResult()) |> ignore
    Async.AwaitTask tcs.Task
