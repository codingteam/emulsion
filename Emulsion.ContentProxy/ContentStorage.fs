module Emulsion.ContentProxy.ContentStorage

open Emulsion.Database
open Emulsion.Database.Models

type MessageIdentity = {
    MessageId: int64
    FileId: string
}

let getOrCreateMessageRecord (context: EmulsionDbContext) (id: MessageIdentity): Async<TelegramContent> =
    failwithf "TODO"
