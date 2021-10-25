module Emulsion.Tests.TestUtils.TestDataStorage

open Microsoft.Data.Sqlite
open Microsoft.EntityFrameworkCore

open Emulsion.Database

let doWithDatabase<'a>(action: IDatabaseSettings -> Async<'a>): Async<'a> = async {
    use connection = new SqliteConnection("Data Source=:memory:")
    let settings =
        { new IDatabaseSettings with
            member _.ContextOptions =
                DbContextOptionsBuilder()
                    .UseSqlite(connection)
                    .Options }
    return! action settings
}
