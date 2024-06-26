// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

module Emulsion.Tests.Database.DatabaseStructureTests

open Microsoft.Data.Sqlite
open Xunit

open Emulsion.Database
open Emulsion.Database.Entities
open Emulsion.TestFramework

[<Fact>]
let ``Unique constraint should hold``(): unit =
    Async.RunSynchronously <| TestDataStorage.doWithDatabase(fun settings -> async {
        let addNewContent(ctx: EmulsionDbContext) =
            let newContent = {
                Id = 0L
                ChatId = 0L
                ChatUserName = "testChat"
                MessageId = 666L
                FileId = "foobar"
                FileName = "file.bin"
                MimeType = "application/octet-stream"
            }
            async {
                do! DataStorage.addAsync ctx.TelegramContents newContent
                let! _ = Async.AwaitTask(ctx.SaveChangesAsync())
                return newContent.Id
            }

        let! id = DataStorage.transaction settings addNewContent
        Assert.NotEqual(0L, id)

        let! ex = Async.AwaitTask(Assert.ThrowsAnyAsync(fun() ->
            Async.StartAsTask(DataStorage.transaction settings addNewContent)
        ))
        let sqlEx = Exceptions.unwrap<SqliteException> ex
        Assert.Contains("UNIQUE constraint failed", sqlEx.Message)
    })
