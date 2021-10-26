namespace Emulsion.Database

open Microsoft.EntityFrameworkCore

type DatabaseSettings =
    { DataSource: string }

    member this.ContextOptions: DbContextOptions =
        DbContextOptionsBuilder()
            .UseSqlite($"Data Source={this.DataSource}")
            .Options
