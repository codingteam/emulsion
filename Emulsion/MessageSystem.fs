module Emulsion.MessageSystem

open System
open System.Threading

type IncomingMessageReceiver = IncomingMessage -> unit

/// The IM message queue. Manages the underlying connection, reconnects when necessary, stores the outgoing messages in
/// a queue and sends them when possible. Redirects the incoming messages to a function passed when starting the queue.
type IMessageSystem =
    /// Starts the IM connection, manages reconnects. Never terminates unless cancelled.
    abstract member Run : IncomingMessageReceiver -> unit

    /// Queues the message to be sent to the IM system when possible.
    abstract member PutMessage : OutgoingMessage -> unit

type RestartContext = {
    cooldown: TimeSpan
    logError: Exception -> unit
    logMessage: string -> unit
}

let internal wrapRun (ctx: RestartContext) (runAsync: Async<unit>) : Async<unit> =
    async {
        while true do
            try
                do! runAsync
            with
            | :? OperationCanceledException -> return ()
            | ex ->
                ctx.logError ex
                ctx.logMessage <| sprintf "Waiting for %A to restart" ctx.cooldown
                do! Async.Sleep(int ctx.cooldown.TotalMilliseconds)
    }

let putMessage (messageSystem: IMessageSystem) (message: OutgoingMessage) =
    messageSystem.PutMessage message

[<AbstractClass>]
type MessageSystemBase(ctx: RestartContext, cancellationToken: CancellationToken) as this =
    let sender = MessageSender.startActivity({
        send = this.Send
        logError = ctx.logError
        cooldown = ctx.cooldown
    }, cancellationToken)

    /// Starts the IM connection, manages reconnects. On cancellation could either throw OperationCanceledException or
    /// return a unit.
    ///
    /// This method will never be called multiple times in parallel on a single instance.
    abstract member RunUntilError : IncomingMessageReceiver -> Async<unit>

    /// Sends a message through the message system. Free-threaded. Could throw exceptions; if throws an exception, then
    /// will be restarted later.
    abstract member Send : OutgoingMessage -> Async<unit>

    interface IMessageSystem with
        member ms.Run receiver =
            Async.RunSynchronously (wrapRun ctx (this.RunUntilError receiver), cancellationToken = cancellationToken)

        member __.PutMessage message =
            MessageSender.send sender message
