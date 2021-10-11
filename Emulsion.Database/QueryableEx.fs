module Emulsion.Database.QueryableEx

open System.Linq

open Microsoft.EntityFrameworkCore
open Microsoft.FSharp.Linq

type QueryBuilder with
    [<CustomOperation("tryExactlyOneAsync")>]
    member _.TryExactlyOneAsync<'T, 'Q>(source: QuerySource<'T, 'Q>): Async<'T option> = async {
        let source = source.Source :?> IQueryable<'T>
        let! ct = Async.CancellationToken
        let! item = Async.AwaitTask(EntityFrameworkQueryableExtensions.SingleOrDefaultAsync(source, ct))

        // We cannot use Option.ofObj here since not every entity type can be marked with AllowNullLiteral.
        match box item with
        | null -> return None
        | _ -> return Some item
    }
