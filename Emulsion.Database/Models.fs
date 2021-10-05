namespace Emulsion.Database.Models

open System.ComponentModel.DataAnnotations

[<CLIMutable>]
type TelegramContent = {
    [<Key>] Id: string
    ChatUsername: string
    MessageId: int64
    FileId: string
}
