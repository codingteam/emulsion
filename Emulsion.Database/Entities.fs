﻿namespace Emulsion.Database.Entities

open System.ComponentModel.DataAnnotations

[<CLIMutable>]
type TelegramContent = {
    [<Key>] Id: int64
    ChatId: int64
    ChatUserName: string
    MessageId: int64
    FileId: string
    FileName: string
    MimeType: string
}
