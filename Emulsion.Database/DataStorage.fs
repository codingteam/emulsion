module Emulsion.Database.DataStorage

open System.Data

open Microsoft.EntityFrameworkCore

open Emulsion.Database.Models

// TODO: databaseSettings: EmulsionDbContext
let getById (databaseSettings: DatabaseSettings) (id: string): Async<TelegramContent> = failwithf "TODO"

let transaction<'a> (settings: IDatabaseSettings) (action: EmulsionDbContext -> Async<'a>): Async<'a> = async {
    use context = new EmulsionDbContext(settings.ContextOptions)
    let! ct = Async.CancellationToken
    use! tran = Async.AwaitTask(context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct))
    let! result = action context
    do! Async.AwaitTask(tran.CommitAsync())
    return result
}

let addAsync<'a when 'a : not struct> (dbSet: DbSet<'a>) (entity: 'a): Async<unit> = async {
    let! ct = Async.CancellationToken
    let! _ = Async.AwaitTask(dbSet.AddAsync(entity, ct).AsTask())
    return ()
}
