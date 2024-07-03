// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

module Emulsion.TestFramework.Lifetimes

open System
open System.Threading.Tasks
open JetBrains.Lifetimes

let WaitForTermination (lt: Lifetime) (timeout: TimeSpan) (message: string): Task =
    let tcs = TaskCompletionSource()
    lt.OnTermination tcs.SetResult |> ignore
    let delay = Task.Delay timeout
    task {
        let! earlier = Task.WhenAny(delay, tcs.Task)
        if earlier <> tcs.Task && not tcs.Task.IsCompleted then
            failwith message
    }
