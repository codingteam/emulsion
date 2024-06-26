// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

module Emulsion.Tests.ContentProxy.ContentStorageTests

open Xunit

open Emulsion.ContentProxy.ContentStorage
open Emulsion.Database
open Emulsion.TestFramework

let private testIdentity = {
    ChatId = 0L
    ChatUserName = "test"
    MessageId = 123L
    FileId = "this_is_file"
    FileName = "file.bin"
    MimeType = "application/octet-stream"
}

let private executeQuery settings =
    DataStorage.transaction settings (fun context ->
        getOrCreateMessageRecord context testIdentity
    )

[<Fact>]
let ``getOrCreateMessageRecord returns an nonzero id``(): unit =
    TestDataStorage.doWithDatabase(fun settings -> async {
        let! newItem = executeQuery settings
        Assert.NotEqual(0L, newItem.Id)
    }) |> Async.RunSynchronously


[<Fact>]
let ``getOrCreateMessageRecord returns a new record``(): unit =
    TestDataStorage.doWithDatabase(fun settings -> async {
        let! item = executeQuery settings
        Assert.Equal(testIdentity.ChatUserName, item.ChatUserName)
        Assert.Equal(testIdentity.MessageId, item.MessageId)
        Assert.Equal(testIdentity.FileId, item.FileId)
    }) |> Async.RunSynchronously

[<Fact>]
let ``getOrCreateMessageRecord returns an existing record``(): unit =
    TestDataStorage.doWithDatabase(fun settings -> async {
        let! existingItem = executeQuery settings
        let! newItem = executeQuery settings
        Assert.Equal(existingItem, newItem)
    }) |> Async.RunSynchronously
