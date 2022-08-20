namespace Emulsion.Tests.TestUtils

open System.Collections.Generic

open Emulsion.Telegram

type TelegramClientMock() =
    let responses = Dictionary<string, FileInfo option>()

    interface ITelegramClient with
        member this.GetFileInfo fileId = async.Return responses[fileId]

    member _.SetResponse(fileId: string, fileInfo: FileInfo option): unit =
        responses[fileId] <- fileInfo
