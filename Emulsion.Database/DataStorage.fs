module Emulsion.Database.DataStorage

open System.Data

open Microsoft.EntityFrameworkCore

let initializeDatabase(context: EmulsionDbContext): Async<unit> = async {
    do! Async.AwaitTask(context.Database.MigrateAsync())
}

let transaction<'a> (settings: DatabaseSettings) (action: EmulsionDbContext -> Async<'a>): Async<'a> = async {
    use context = new EmulsionDbContext(settings.ContextOptions)
    let! ct = Async.CancellationToken
    use! tran = Async.AwaitTask(context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct))
    let! result = action context
    do! Async.AwaitTask(tran.CommitAsync(ct))
    let! _ = Async.AwaitTask(context.SaveChangesAsync(ct))
    return result
}

let addAsync<'a when 'a : not struct> (dbSet: DbSet<'a>) (entity: 'a): Async<unit> = async {
    let! ct = Async.CancellationToken
    let! _ = Async.AwaitTask(dbSet.AddAsync(entity, ct).AsTask())
    return ()
}
