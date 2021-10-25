namespace Emulsion.Database

open Emulsion.Database.Models
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Design

type EmulsionDbContext(options: DbContextOptions) =
    inherit DbContext(options)

    [<DefaultValue>] val mutable telegramContents: DbSet<TelegramContent>
    member this.TelegramContents with get() = this.telegramContents and set v = this.telegramContents <- v

/// This type is used by the EFCore infrastructure when creating a new migration.
type EmulsionDbContextDesignFactory() =
    interface IDesignTimeDbContextFactory<EmulsionDbContext> with
        member this.CreateDbContext _ =
            let options =
                DbContextOptionsBuilder()
                    .UseSqlite("Data Source=:memory:")
                    .Options
            new EmulsionDbContext(options)
