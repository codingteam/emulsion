module Emulsion.Tests.MessageSystem

open Xunit

open System
open System.Threading
open Emulsion

let private performTest expectedStage runBody =
    use cts = new CancellationTokenSource()
    let mutable stage = 0
    let run ct =
        stage <- stage + 1
        runBody cts ct stage
    MessageSystem.wrapRun cts.Token run ignore
    Assert.Equal(expectedStage, stage)

[<Fact>]
let ``wrapRun should restart the activity on error``() =
    performTest 2 (fun cts _ stage ->
        match stage with
        | 1 -> raise <| Exception()
        | 2 -> cts.Cancel()
    )

[<Fact>]
let ``wrapRun should not restart on OperationCanceledException``() =
    performTest 1 (fun cts ct _ ->
        cts.Cancel()
        ct.ThrowIfCancellationRequested()
    )

[<Fact>]
let ``wrapRun should not restart on token.Cancel()``() =
    performTest 4 (fun cts _ stage ->
        if stage > 3 then
            cts.Cancel()
    )
