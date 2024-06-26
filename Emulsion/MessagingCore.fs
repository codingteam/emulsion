// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

namespace Emulsion

open System
open System.Threading.Channels
open System.Threading.Tasks
open Emulsion.Messaging
open Emulsion.Messaging.MessageSystem
open JetBrains.Collections.Viewable
open JetBrains.Lifetimes
open Microsoft.FSharp.Core
open Serilog

type MessagingCore(
    lifetime: Lifetime,
    logger: ILogger,
    archive: IMessageArchive option
) =
    let messageProcessedSuccessfully = Signal<Unit>()
    let processMessage telegram xmpp message ct =
        task {
            match archive with
            | Some a -> do! Async.StartAsTask(a.Archive message, cancellationToken = ct)
            | None -> ()

            match message with
            | TelegramMessage msg -> putMessage xmpp (OutgoingMessage msg)
            | XmppMessage msg -> putMessage telegram (OutgoingMessage msg)

            messageProcessedSuccessfully.Fire(())
        }

    let messages = Channel.CreateUnbounded()
    do lifetime.OnTermination(fun () -> messages.Writer.Complete()) |> ignore

    let messageProcessingError = Signal<Unit>()
    let processLoop telegram xmpp: Task = task {
        logger.Information("Core workflow starting.")

        let ct = lifetime.ToCancellationToken()
        while lifetime.IsAlive do
            try
                let! m = messages.Reader.ReadAsync ct
                do! lifetime.ExecuteAsync(fun() -> processMessage telegram xmpp m ct)
            with
            | :? OperationCanceledException -> ()
            | error ->
                logger.Error(error, "Core workflow exception.")
                messageProcessingError.Fire(())

        logger.Information("Core workflow terminating.")
    }

    let messageCannotBeReceived = Signal<Unit>()
    member _.MessageProcessedSuccessfully: ISource<Unit> = messageProcessedSuccessfully
    member _.MessageCannotBeReceived: ISource<Unit> = messageCannotBeReceived
    member _.MessageProcessingError: ISource<Unit> = messageProcessingError
    member val ProcessingTask = None with get, set

    member this.Start(telegram: IMessageSystem, xmpp: IMessageSystem) =
        this.ProcessingTask <- Some(Task.Run(fun() -> processLoop telegram xmpp))

    member _.ReceiveMessage(message: IncomingMessage): unit =
        let result = messages.Writer.TryWrite message
        if not result then
            logger.Error("Write status to core channel should always be true, but it is {Status}.", result)
            messageCannotBeReceived.Fire(())
