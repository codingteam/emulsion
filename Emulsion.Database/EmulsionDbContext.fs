namespace Emulsion.Database

open Emulsion.Database.Models
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Design

type EmulsionDbContext(dataSource: string) =
    inherit DbContext()

    [<DefaultValue>] val mutable telegramContents: DbSet<TelegramContent>
    member this.TelegramContents with get() = this.telegramContents and set v = this.telegramContents <- v

    override _.OnConfiguring options =
        options.UseSqlite($"Data Source={dataSource};") |> ignore

/// This type is used by the EFCore infrastructure when creating a new migration.
type EmulsionDbContextDesignFactory() =
    interface IDesignTimeDbContextFactory<EmulsionDbContext> with
        member this.CreateDbContext _ =
            new EmulsionDbContext(":memory:")
