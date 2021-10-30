module Emulsion.Database.DataStorage

open System.Data

open Microsoft.EntityFrameworkCore

let transaction<'a> (settings: DatabaseSettings) (action: EmulsionDbContext -> Async<'a>): Async<'a> = async {
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
