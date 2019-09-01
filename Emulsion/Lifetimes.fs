module Emulsion.Lifetimes

open System
open System.Threading
open System.Threading.Tasks

type LifetimeDefinition(cts: CancellationTokenSource) =
    new() = new LifetimeDefinition(new CancellationTokenSource())
    member __.Lifetime: Lifetime = Lifetime(cts.Token)
    member __.Terminate(): unit = cts.Cancel()
    interface IDisposable with
        member __.Dispose() = cts.Dispose()
and Lifetime(token: CancellationToken) =
    member __.Token: CancellationToken = token
    member __.CreateNested(): LifetimeDefinition =
        let cts = CancellationTokenSource.CreateLinkedTokenSource token
        new LifetimeDefinition(cts)
    member __.OnTermination(action: Action): unit =
        token.Register action |> ignore

    /// Schedules a termination action, and returns an IDisposable. Whenever this instance is disposed, the action will
    /// be removed from scheduled on cancellation.
    member __.OnTerminationRemovable(action: Action): IDisposable =
        upcast token.Register action

let nestedTaskCompletionSource<'T>(lifetime: Lifetime): TaskCompletionSource<'T> =
    let tcs = new TaskCompletionSource<'T>()

    // As an optimization, we'll remove the action after the task has been completed to clean up the memory:
    let action = lifetime.OnTerminationRemovable(fun () -> tcs.TrySetCanceled() |> ignore)
    tcs.Task.ContinueWith(fun (t: Task<'T>) -> action.Dispose()) |> ignore

    tcs
