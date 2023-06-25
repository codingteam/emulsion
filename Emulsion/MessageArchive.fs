namespace Emulsion

open System

open Emulsion.Database
open Emulsion.Database.Entities
open Emulsion.Messaging

type MessageArchive(databaseSettings: DatabaseSettings option) =

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

    member _.Archive(message: IncomingMessage): Async<unit> =
        match databaseSettings with
        | None -> async.Return()
        | Some database ->
            let message = convert message
            DataStorage.transaction database (fun context ->
                DataStorage.addAsync context.ArchiveEntries message
            )
