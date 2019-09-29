module Emulsion.MessageSystem

open System
open System.Threading

open Serilog

type IncomingMessageReceiver = IncomingMessage -> unit

/// The IM message queue. Manages the underlying connection, reconnects when necessary, stores the outgoing messages in
/// a queue and sends them when possible. Redirects the incoming messages to a function passed when starting the queue.
type IMessageSystem =
    /// Starts the IM connection, manages reconnects. Never terminates unless cancelled.
    abstract member RunSynchronously : IncomingMessageReceiver -> unit

    /// Queues the message to be sent to the IM system when possible.
    abstract member PutMessage : OutgoingMessage -> unit

type ServiceContext = {
    RestartCooldown: TimeSpan
    Logger: ILogger
}

let internal wrapRun (ctx: ServiceContext) (runAsync: Async<unit>) : Async<unit> =
    async {
        while true do
            try
                do! runAsync
            with
            | :? OperationCanceledException -> return ()
            | ex ->
                ctx.Logger.Error(ex, "Non-terminating message system error")
                ctx.Logger.Information("Waiting for {RestartCooldown} to restart the message system",
                                       ctx.RestartCooldown)
                do! Async.Sleep(int ctx.RestartCooldown.TotalMilliseconds)
    }

let putMessage (messageSystem: IMessageSystem) (message: OutgoingMessage) =
    messageSystem.PutMessage message

[<AbstractClass>]
type MessageSystemBase(ctx: ServiceContext, cancellationToken: CancellationToken) as this =
    let sender = MessageSender.startActivity({
        Send = this.Send
        Logger = ctx.Logger
        RestartCooldown = ctx.RestartCooldown
    }, cancellationToken)

    /// Implements the two-phase run protocol.
    ///
    /// First, the parent async workflow resolves when the connection has been established, and the system is ready to
    /// receive outgoing messages.
    ///
    /// The nested async workflow is a message loop inside of a system. While this second workflow is executing, the
    /// system is expected to receive the messages.
    ///
    /// Any of these workflows could either throw OperationCanceledException or return a unit on cancellation.
    ///
    /// This method will never be called multiple times in parallel on a single instance.
    abstract member RunUntilError : IncomingMessageReceiver -> Async<Async<unit>>

    /// Sends a message through the message system. Free-threaded. Could throw exceptions; if throws an exception, then
    /// will be restarted later.
    abstract member Send : OutgoingMessage -> Async<unit>

    /// Runs the message system loop asynchronously. Should never terminate unless cancelled.
    abstract member RunAsync : IncomingMessageReceiver -> Async<unit>
    default _.RunAsync receiver = async {
        // While this line executes, the system isn't yet started and isn't ready to accept the messages:
        let! runLoop = this.RunUntilError receiver
        MessageSender.setReadyToAcceptMessages sender true
        try
            do! runLoop
        finally
            MessageSender.setReadyToAcceptMessages sender false
    }

    interface IMessageSystem with
        member _.RunSynchronously receiver =
            let runner = this.RunAsync receiver
            Async.RunSynchronously(wrapRun ctx runner, cancellationToken = cancellationToken)

        member _.PutMessage message =
            MessageSender.send sender message
