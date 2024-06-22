// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

namespace Emulsion.Database

open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Design

open Emulsion.Database.Entities

type EmulsionDbContext(options: DbContextOptions) =
    inherit DbContext(options)

    [<DefaultValue>] val mutable private telegramContents: DbSet<TelegramContent>
    member this.TelegramContents with get() = this.telegramContents and set v = this.telegramContents <- v

    [<DefaultValue>] val mutable private archiveEntries: DbSet<ArchiveEntry>
    member this.ArchiveEntries with get() = this.archiveEntries and set v = this.archiveEntries <- v

/// This type is used by the EFCore infrastructure when creating a new migration.
type EmulsionDbContextDesignFactory() =
    interface IDesignTimeDbContextFactory<EmulsionDbContext> with
        member this.CreateDbContext _ =
            let options =
                DbContextOptionsBuilder()
                    .UseSqlite("Data Source=:memory:")
                    .Options
            new EmulsionDbContext(options)
