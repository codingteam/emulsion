// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

namespace Emulsion.Web

open System
open System.Collections.Generic
open System.Threading.Tasks

open Microsoft.AspNetCore.Mvc
open Microsoft.EntityFrameworkCore

open Emulsion.Database
open Emulsion.Database.Entities

type MessageStatistics = {
    MessageCount: int
}

type Message = {
    MessageSystemId: string
    Sender: string
    DateTime: DateTimeOffset
    Text: string
}

[<ApiController>]
[<Route("api/history")>]
type HistoryController(context: EmulsionDbContext) =
    inherit ControllerBase()

    let convertMessage(entry: ArchiveEntry) =
        {
            MessageSystemId = entry.MessageSystemId
            Sender = entry.Sender
            DateTime = entry.DateTime
            Text = entry.Text
        }

    [<HttpGet("statistics")>]
    member this.GetStatistics(): Task<MessageStatistics> = task {
        let! count = context.ArchiveEntries.CountAsync()
        return {
            MessageCount = count
        }
    }

    [<HttpGet("messages")>]
    member this.GetMessages(offset: int, limit: int): Task<IEnumerable<Message>> = task {
        let! messages =
            (query {
                for entry in context.ArchiveEntries do
                sortBy entry.Id
                skip offset
                take limit
            }).ToListAsync()
        return messages |> Seq.map convertMessage
    }
