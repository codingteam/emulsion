namespace Emulsion

open System
open System.Threading.Channels
open System.Threading.Tasks
open Emulsion.Messaging
open Emulsion.Messaging.MessageSystem
open JetBrains.Lifetimes
open Serilog

type MessagingCore(
    lifetime: Lifetime,
    logger: ILogger,
    archive: MessageArchive option
) =
    let processMessage telegram xmpp message ct =
        task {
            match archive with
            | Some a -> do! Async.StartAsTask(a.Archive message, cancellationToken = ct)
            | None -> ()

            match message with
            | TelegramMessage msg -> putMessage xmpp (OutgoingMessage msg)
            | XmppMessage msg -> putMessage telegram (OutgoingMessage msg)
        }

    let messages = Channel.CreateUnbounded()
    do lifetime.OnTermination(fun () -> messages.Writer.Complete()) |> ignore

    let processLoop telegram xmpp: Task = task {
        logger.Information("Core workflow starting.")

        let ct = lifetime.ToCancellationToken()
        while lifetime.IsAlive do
            try
                let! m = messages.Reader.ReadAsync ct
                do! lifetime.ExecuteAsync(fun() -> processMessage telegram xmpp m ct)
            with
            | :? OperationCanceledException -> ()
            | error -> logger.Error(error, "Core workflow exception.")

        logger.Information("Core workflow terminating.")
    }

    member _.Start(telegram: IMessageSystem, xmpp: IMessageSystem) =
        Task.Run(fun() -> processLoop telegram xmpp) |> ignore

    member _.ReceiveMessage(message: IncomingMessage): unit =
        let result = messages.Writer.TryWrite message
        if not result then
            logger.Error("Write status to core channel should always be true, but it is {Status}.", result)
