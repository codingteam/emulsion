﻿module Emulsion.ContentProxy.ContentStorage

open Emulsion.Database
open Emulsion.Database.DataStorage
open Emulsion.Database.Entities
open Emulsion.Database.QueryableEx

type MessageContentIdentity = {
    ChatUserName: string
    MessageId: int64
    FileId: string
}

let getOrCreateMessageRecord (context: EmulsionDbContext) (id: MessageContentIdentity): Async<TelegramContent> = async {
    let! existingItem =
        query {
            for content in context.TelegramContents do
            where (content.ChatUserName = id.ChatUserName
                   && content.MessageId = id.MessageId
                   && content.FileId = id.FileId)
        } |> tryExactlyOneAsync
    match existingItem with
    | None ->
        let newItem = {
            Id = 0L
            ChatUserName = id.ChatUserName
            MessageId = id.MessageId
            FileId = id.FileId
        }
        do! addAsync context.TelegramContents newItem
        return newItem
    | Some item -> return item
}

let getById (context: EmulsionDbContext) (id: int64): Async<TelegramContent option> = async {
    return! query {
        for content in context.TelegramContents do
        where (content.Id = id)
    } |> tryExactlyOneAsync
}
