namespace Emulsion.Database

open EntityFrameworkCore.FSharp.Extensions
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Design

open Emulsion.Database.Entities

type EmulsionDbContext(options: DbContextOptions) =
    inherit DbContext(options)

    override _.OnModelCreating builder =
        builder.RegisterOptionTypes()

    [<DefaultValue>] val mutable private telegramContents: DbSet<TelegramContent>
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
