// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

namespace Emulsion.Database

open Microsoft.EntityFrameworkCore

type DatabaseSettings =
    { DataSource: string }

    member this.ContextOptions: DbContextOptions =
        DbContextOptionsBuilder()
            .UseSqlite($"Data Source={this.DataSource}")
            .Options
