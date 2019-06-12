module Emulsion.MessageSystem

open System
open System.Threading

type IncomingMessageReceiver = IncomingMessage -> unit

/// The IM message queue. Manages the underlying connection, reconnects when necessary, stores the outgoing messages in
/// a queue and sends them when possible. Redirects the incoming messages to a function passed when starting the queue.
type IMessageSystem =
    /// Starts the IM connection, manages reconnects. On cancellation could either throw OperationCanceledException or
    /// return a unit.
    abstract member Run : IncomingMessageReceiver -> CancellationToken -> unit

    /// Queues the message to be sent to the IM system when possible.
    abstract member PutMessage : OutgoingMessage -> unit

type RestartContext = {
    token: CancellationToken
    cooldown: TimeSpan
    logError: Exception -> unit
    logMessage: string -> unit
}

let wrapRun (ctx: RestartContext) (run: CancellationToken -> unit) : unit =
    while not ctx.token.IsCancellationRequested do
        try
            run ctx.token
        with
        | :? OperationCanceledException -> ()
        | ex ->
            ctx.logError ex
            ctx.logMessage <| sprintf "Waiting for %A to restart" ctx.cooldown
            Thread.Sleep ctx.cooldown
