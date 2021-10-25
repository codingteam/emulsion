module Emulsion.Tests.ContentProxy.ContentStorageTests

open Xunit

open Emulsion.ContentProxy.ContentStorage
open Emulsion.Database
open Emulsion.Tests.TestUtils

let private testIdentity = {
    ChatUserName = "test"
    MessageId = 123L
    FileId = "this_is_file"
}

let private executeQuery settings =
    DataStorage.transaction settings (fun context ->
        getOrCreateMessageRecord context testIdentity
    )

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
