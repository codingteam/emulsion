// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

module Emulsion.TestFramework.TestDataStorage

open System.IO

open Emulsion.Database

let doWithDatabase<'a>(action: DatabaseSettings -> Async<'a>): Async<'a> = async {
    let databasePath = Path.GetTempFileName()
    let settings = { DataSource = databasePath }

    do! async {
        use context = new EmulsionDbContext(settings.ContextOptions)
        return! DataStorage.initializeDatabase context
    }

    return! action settings
}
