module Emulsion.Database.Initializer

open Microsoft.EntityFrameworkCore

let initializeDatabase(context: EmulsionDbContext): Async<unit> = async {
    do! Async.AwaitTask(context.Database.MigrateAsync())
}
