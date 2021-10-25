namespace Emulsion.Database

open Microsoft.EntityFrameworkCore

type IDatabaseSettings =
    abstract member ContextOptions: DbContextOptions

type DatabaseSettings =
    { DataSource: string }
    interface IDatabaseSettings with
        member this.ContextOptions: DbContextOptions =
            DbContextOptionsBuilder()
                .UseSqlite($"Data Source={this.DataSource}")
                .Options
