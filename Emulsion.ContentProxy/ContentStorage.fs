module Emulsion.ContentProxy.ContentStorage

open Emulsion.Database
open Emulsion.Database.DataStorage
open Emulsion.Database.Models
open Emulsion.Database.QueryableEx

type MessageIdentity = {
    ChatUserName: string
    MessageId: int64
    FileId: string
}

let getOrCreateMessageRecord (context: EmulsionDbContext) (id: MessageIdentity): Async<TelegramContent> = async {
    let! existingItem =
        query {
            for content in context.TelegramContents do
            where (content.MessageId = id.MessageId && content.FileId = id.FileId)
            tryExactlyOneAsync
        }
    match existingItem with
    | None ->
        let newItem = {
            Id = 0L
            ChatUsername = id.ChatUserName
            MessageId = id.MessageId
            FileId = id.FileId
        }
        do! addAsync context.TelegramContents newItem
        return newItem
    | Some item -> return item
}
