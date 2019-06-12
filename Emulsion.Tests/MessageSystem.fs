module Emulsion.Tests.MessageSystem

open System
open System.Threading

open Xunit

open Emulsion
open Emulsion.MessageSystem

let private performTest expectedStage runBody =
    use cts = new CancellationTokenSource()
    let mutable stage = 0
    let run ct =
        stage <- stage + 1
        runBody cts ct stage
    let context = {
        token = cts.Token
        cooldown = TimeSpan.Zero
        logError = ignore
        logMessage = ignore
    }
    MessageSystem.wrapRun context run
    Assert.Equal(expectedStage, stage)

[<Fact>]
let ``wrapRun should restart the activity on error``() =
    performTest 2 (fun cts _ stage ->
        match stage with
        | 1 -> raise <| Exception()
        | 2 -> cts.Cancel()
        | _ -> failwith "Impossible"
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
