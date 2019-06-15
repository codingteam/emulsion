module Emulsion.MessageSystem

open System
open System.Threading

type IncomingMessageReceiver = Message -> unit

/// The IM message queue. Manages the underlying connection, reconnects when necessary, stores the outgoing messages in
/// a queue and sends them when possible. Redirects the incoming messages to a function passed when starting the queue.
type IMessageSystem =
    /// Starts the IM connection, manages reconnects. Never terminates unless cancelled.
    abstract member Run : IncomingMessageReceiver -> CancellationToken -> unit

    /// Queues the message to be sent to the IM system when possible.
    abstract member PutMessage : OutgoingMessage -> unit

type RestartContext = {
    cooldown: TimeSpan
    logError: Exception -> unit
    logMessage: string -> unit
}

let internal wrapRun (ctx: RestartContext) (token: CancellationToken) (run: CancellationToken -> unit) : unit =
    while not token.IsCancellationRequested do
        try
            run token
        with
        | :? OperationCanceledException -> ()
        | ex ->
            ctx.logError ex
            ctx.logMessage <| sprintf "Waiting for %A to restart" ctx.cooldown
            Thread.Sleep ctx.cooldown

let putMessage (messageSystem: IMessageSystem) (message: OutgoingMessage) =
    messageSystem.PutMessage message

[<AbstractClass>]
type MessageSystemBase(restartContext: RestartContext) as this =
    let sender = MessageSender.activity {
        send = this.Send
        logError = restartContext.logError
        cooldown = restartContext.cooldown
    }

    /// Starts the IM connection, manages reconnects. On cancellation could either throw OperationCanceledException or
    /// return a unit.
    abstract member Run : IncomingMessageReceiver -> CancellationToken -> unit

    /// Sends a message through the message system. Free-threaded. Could throw exceptions; if throws an exception, then
    /// will be restarted later.
    abstract member Send : OutgoingMessage -> Async<unit>

    interface IMessageSystem with
        member ms.Run receiver token =
            wrapRun restartContext token (this.Run receiver)
        member __.PutMessage message =
            MessageSender.send sender message
