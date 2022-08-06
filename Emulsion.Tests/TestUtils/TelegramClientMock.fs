namespace Emulsion.Tests.TestUtils

open System
open System.Collections.Generic

open Emulsion.Telegram

type TelegramClientMock() =
    let responses = Dictionary<string, Uri>()

    interface ITelegramClient with
        member this.GetTemporaryFileLink fileId = async.Return responses[fileId]

    member _.SetResponse(fileId: string, uri: Uri): unit =
        responses[fileId] <- uri
