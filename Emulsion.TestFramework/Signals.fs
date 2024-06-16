module Emulsion.TestFramework.Signals

open System
open System.Threading.Tasks
open JetBrains.Collections.Viewable
open JetBrains.Lifetimes

let WaitWithTimeout (lt: Lifetime) (source: ISource<Unit>) (timeout: TimeSpan) (message: string): Task = task {
    let delay = Task.Delay timeout
    let waiter = source.NextValueAsync lt
    let! _ = Task.WhenAny(waiter, delay)
    if not waiter.IsCompleted then
        failwithf $"Timeout of {timeout} when waiting for {message}."
    do! Task.Yield() // to untangle further actions from the signal task termination
}
