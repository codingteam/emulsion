module Emulsion.Tests.TestUtils.Waiter

open System
open System.Threading

let defaultTimeout = TimeSpan.FromSeconds 30.0
let shortTimeout = TimeSpan.FromSeconds 1.0

let waitForItemCount (buffer: LockedBuffer<_>) count (timeout: TimeSpan) =
    SpinWait.SpinUntil((fun () -> buffer.Count() = count), timeout)
