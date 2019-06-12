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

let internal wrapRun (token: CancellationToken) (run: CancellationToken -> unit) (log: Exception -> unit) : unit =
    while not token.IsCancellationRequested do
        try
            run token
        with
        | :? OperationCanceledException -> ()
        | ex -> log ex
