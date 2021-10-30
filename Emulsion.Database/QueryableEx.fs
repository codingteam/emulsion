module Emulsion.Database.QueryableEx

open System.Linq

open Microsoft.EntityFrameworkCore

let tryExactlyOneAsync<'a>(source: IQueryable<'a>): Async<'a option> = async {
    let! ct = Async.CancellationToken
    let! item = Async.AwaitTask(EntityFrameworkQueryableExtensions.SingleOrDefaultAsync(source, ct))

    // We cannot use Option.ofObj here since not every entity type can be marked with AllowNullLiteral.
    match box item with
    | null -> return None
    | _ -> return Some item
}

let exactlyOneAsync<'a>(source: IQueryable<'a>): Async<'a> = async {
    let! ct = Async.CancellationToken
    return! Async.AwaitTask(EntityFrameworkQueryableExtensions.SingleAsync(source, ct))
}
