namespace Emulsion.Database.Models

open System
open System.ComponentModel.DataAnnotations

[<CLIMutable>]
type TelegramContent = {
    [<Key>] Id: Guid
}
