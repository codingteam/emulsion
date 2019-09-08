module Emulsion.Tests.ExceptionUtilsTests

open System

open Emulsion
open Xunit

[<Fact>]
let ``reraise works in sync code``(): unit =
    let nestedStacktrace() =
        raise <| Exception("Foo")
    let thrown =
        try
            nestedStacktrace()
            null
        with
        | ex -> ex
    let rethrown = Assert.Throws<Exception>(fun () -> ExceptionUtils.reraise thrown |> ignore)
    Assert.Contains("nestedStacktrace", rethrown.StackTrace)

[<Fact>]
let ``reraise works in async code``(): unit =
    let nestedStacktrace() =
        raise <| Exception("Foo")

    let ex = Assert.Throws<Exception>(fun () ->
        async {
            try
                nestedStacktrace()
            with
            | ex ->
                ExceptionUtils.reraise ex
        } |> Async.RunSynchronously
    )
    Assert.Contains("nestedStacktrace", ex.StackTrace)
