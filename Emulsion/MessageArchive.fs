// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

namespace Emulsion

open System

open Emulsion.Database
open Emulsion.Database.Entities
open Emulsion.Messaging

type IMessageArchive =
    abstract member Archive: IncomingMessage -> Async<unit>

type MessageArchive(database: DatabaseSettings) =

    let convert message =
        let body, messageSystemId =
            match message with
            | XmppMessage msg -> msg, "XMPP"
            | TelegramMessage msg -> msg, "Telegram"
        let sender, text =
            match body with
            | Authored msg -> msg.author, msg.text
            | Event e -> "", e.text

        {
            Id = 0L
            MessageSystemId = messageSystemId
            DateTime = DateTimeOffset.UtcNow
            Sender = sender
            Text = text
        }

    interface IMessageArchive with
        member _.Archive(message: IncomingMessage): Async<unit> =
            let message = convert message
            DataStorage.transaction database (fun context ->
                DataStorage.addAsync context.ArchiveEntries message
            )
