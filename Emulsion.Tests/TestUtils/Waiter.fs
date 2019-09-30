module Emulsion.Tests.TestUtils.Waiter

open System
open System.Threading

let defaultTimeout = TimeSpan.FromSeconds 30.0
let shortTimeout = TimeSpan.FromSeconds 1.0

let waitForItemCountCond (buffer: LockedBuffer<_>) (condition: int -> bool) (timeout: TimeSpan) =
    SpinWait.SpinUntil((fun () -> condition(buffer.Count())), timeout)

let waitForItemCount (buffer: LockedBuffer<_>) count (timeout: TimeSpan) =
    waitForItemCountCond buffer (fun c -> c = count) timeout
