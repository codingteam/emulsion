module Emulsion.Tests.Database.InitializerTests

open System.IO

open Xunit

open Emulsion.Database

[<Fact>]
let ``Database initialization``(): unit =
    async {
        let databasePath = Path.Combine(Path.GetTempPath(), "emulsion-test.db")
        let settings = { DataSource = databasePath }
        use context = new EmulsionDbContext(settings.ContextOptions)
        let! _ = Async.AwaitTask(context.Database.EnsureDeletedAsync())
        do! DataStorage.initializeDatabase context
    } |> Async.RunSynchronously
