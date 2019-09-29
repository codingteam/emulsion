module Emulsion.MessageSender

open System
open System.Threading

open FSharpx.Collections
open Serilog

type MessageSenderContext = {
    Send: OutgoingMessage -> Async<unit>
    Logger: ILogger
    RestartCooldown: TimeSpan
}

type private State = {
    Messages: Queue<OutgoingMessage>
    ReadyToAcceptMessages: bool
} with static member initial = { Messages = Queue.empty; ReadyToAcceptMessages = false }

let private trySendMessage ctx msg = async {
    try
        do! ctx.Send msg
        return true
    with
    | ex ->
        ctx.Logger.Error(ex, "Error when trying to send message {Message}", msg)
        return false
}

let private processState ctx (state: State) = async {
    if not state.ReadyToAcceptMessages then
        return state
    else
        match state.Messages with
        | Queue.Nil -> return state
        | Queue.Cons(message, rest) ->
            let! success = trySendMessage ctx message
            if not success then
                ctx.Logger.Information("Waiting for {RestartCooldown} to resume processing output message queue",
                                       ctx.RestartCooldown)
                do! Async.Sleep(int ctx.RestartCooldown.TotalMilliseconds)
            let newState =
                if success
                then { state with Messages = rest }
                else state // leave the message in the queue
            return newState
}

type Event =
| QueueMessage of OutgoingMessage
| SetReceiveStatus of bool

type Sender = MailboxProcessor<Event>
let private receiver ctx (inbox: Sender) =
    let rec loop (state: State) = async {
        ctx.Logger.Debug("Current queue state: {State}", state)

        let processIncomingMessage msg = async {
            let newState =
                match msg with
                | QueueMessage m ->
                    let newMessages = Queue.conj m state.Messages
                    { state with Messages = newMessages }
                | SetReceiveStatus status ->
                    { state with ReadyToAcceptMessages = status }
            return! processState ctx newState
        }

        let blockAndProcessIncomingMessage() = async {
            let! msg = inbox.Receive()
            return! processIncomingMessage msg
        }

        let! nextState =
            match state.ReadyToAcceptMessages, state.Messages with
            | false, _ -> // We aren't permitted to send any messages, we have nothing other to do than block on the
                          // message queue.
                blockAndProcessIncomingMessage()
            | true, Queue.Cons _ -> // We're permitted to send a message and the queue is not empty.
                // Peek the queued message: maybe it's the `SetReceiveStatus false` which should have the priority over
                // everything else. If there're no incoming messages, then just process the current state as usual.
                async {
                    match! inbox.TryReceive 0 with
                    | Some msg -> return! processIncomingMessage msg
                    | None -> return! processState ctx state
                }
            | true, Queue.Nil -> // We're allowed to send a message, but the queue is empty. We have nothing to send,
                                 // thus we have nothing to do other than to block on the message queue.
                blockAndProcessIncomingMessage()

        return! loop nextState
    }
    loop State.initial

let startActivity(ctx: MessageSenderContext, token: CancellationToken): Sender =
    MailboxProcessor.Start(receiver ctx, token)

let setReadyToAcceptMessages(activity: Sender): bool -> unit = SetReceiveStatus >> activity.Post
let send(activity: Sender): OutgoingMessage -> unit = QueueMessage >> activity.Post
