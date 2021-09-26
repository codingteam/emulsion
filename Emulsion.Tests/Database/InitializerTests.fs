module Emulsion.Tests.Database.InitializerTests

open System.IO
open Emulsion.Database
open Xunit

[<Fact>]
let ``Database initialization``(): unit =
    async {
        let databasePath = Path.Combine(Path.GetTempPath(), "emulsion-test.db")
        use context = new EmulsionDbContext(databasePath)
        let! _ = Async.AwaitTask(context.Database.EnsureDeletedAsync())
        do! Initializer.initializeDatabase context
    } |> Async.RunSynchronously
