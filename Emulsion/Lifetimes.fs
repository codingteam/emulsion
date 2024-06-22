// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

module Emulsion.Lifetimes

open System.Threading.Tasks

open JetBrains.Lifetimes

let awaitTermination(lifetime: Lifetime): Async<unit> =
    let tcs = TaskCompletionSource()
    lifetime.OnTermination(fun () -> tcs.SetResult()) |> ignore
    Async.AwaitTask tcs.Task
