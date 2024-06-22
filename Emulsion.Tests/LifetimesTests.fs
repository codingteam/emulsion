// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

module Emulsion.Tests.LifetimesTests

open JetBrains.Lifetimes

open Xunit

open Emulsion.Lifetimes

[<Fact>]
let ``awaitTermination completes after the parent lifetime is terminated``(): unit =
    use ld = Lifetime.Define()
    let task = Async.StartAsTask <| awaitTermination ld.Lifetime
    Assert.False task.IsCompleted
    ld.Terminate()
    task.GetAwaiter().GetResult() |> ignore
