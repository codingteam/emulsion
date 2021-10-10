module Emulsion.Database.DataStorage

open System.Data

open Microsoft.EntityFrameworkCore

open Emulsion.Database.Models

// TODO: databaseSettings: EmulsionDbContext
let getById (databaseSettings: DatabaseSettings) (id: string): Async<TelegramContent> = failwithf "TODO"

let transaction<'a> (settings: DatabaseSettings) (action: EmulsionDbContext -> Async<'a>): Async<'a> = async {
    use context = new EmulsionDbContext(settings.DataSource)
    let! ct = Async.CancellationToken
    use! tran = Async.AwaitTask(context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct))
    let! result = action context
    do! Async.AwaitTask(tran.CommitAsync())
    return result
}
