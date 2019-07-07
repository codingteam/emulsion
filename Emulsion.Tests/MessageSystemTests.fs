module Emulsion.Tests.MessageSystemTests

open System
open System.Threading

open Xunit

open Emulsion
open Emulsion.MessageSystem

let private performTest expectedStage runBody =
    use cts = new CancellationTokenSource()
    let mutable stage = 0
    let run = async {
        stage <- stage + 1
        runBody cts stage
    }
    let context = {
        cooldown = TimeSpan.Zero
        logError = ignore
        logMessage = ignore
    }

    try
        Async.RunSynchronously(MessageSystem.wrapRun context run, cancellationToken = cts.Token)
    with
    | :? OperationCanceledException -> ()

    Assert.Equal(expectedStage, stage)

[<Fact>]
let ``wrapRun should restart the activity on error``() =
    performTest 2 (fun cts stage ->
        match stage with
        | 1 -> raise <| Exception()
        | 2 -> cts.Cancel()
        | _ -> failwith "Impossible"
    )

[<Fact>]
let ``wrapRun should not restart on OperationCanceledException``() =
    performTest 1 (fun cts _ ->
        cts.Cancel()
        cts.Token.ThrowIfCancellationRequested()
    )

[<Fact>]
let ``wrapRun should not restart on token.Cancel()``() =
    performTest 4 (fun cts stage ->
        if stage > 3 then
            cts.Cancel()
    )
