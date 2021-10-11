namespace Emulsion.Database.Models

open System.ComponentModel.DataAnnotations

[<CLIMutable>]
type TelegramContent = {
    [<Key>] Id: int64
    ChatUsername: string
    MessageId: int64
    FileId: string
}
