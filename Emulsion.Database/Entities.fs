// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

namespace Emulsion.Database.Entities

open System
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

[<CLIMutable>]
type ArchiveEntry = {
    [<Key>] Id: int64
    MessageSystemId: string
    DateTime: DateTimeOffset
    Sender: string
    Text: string
}
