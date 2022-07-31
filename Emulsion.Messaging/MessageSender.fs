module Emulsion.Messaging.MessageSender

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
    ClientReadyToSendMessages: bool
} with static member initial = { Messages = Queue.empty; ClientReadyToSendMessages = false }

let private trySendMessage ctx msg = async {
    try
        do! ctx.Send msg
        return true
    with
    | ex ->
        ctx.Logger.Error(ex, "Error when trying to send message {Message}", msg)
        return false
}

let private tryProcessTopMessage ctx (state: State) = async {
    if not state.ClientReadyToSendMessages then
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

let private updateState state msg =
    match msg with
    | QueueMessage m ->
        let newMessages = Queue.conj m state.Messages
        { state with Messages = newMessages }
    | SetReceiveStatus status ->
        { state with ClientReadyToSendMessages = status }

type Sender = MailboxProcessor<Event>
let internal receiver (ctx: MessageSenderContext) (inbox: Sender): Async<unit> =
    let rec loop (state: State) = async {
        ctx.Logger.Debug("Current queue state: {State}", state)

        let blockAndProcessNextIncomingMessage() = async {
            let! msg = inbox.Receive()
            return! loop (updateState state msg)
        }

        // Always process the incoming queue first if there're anything there:
        match! inbox.TryReceive 0 with
        | Some msg ->
            return! loop (updateState state msg)
        | None ->
            match state.ClientReadyToSendMessages, state.Messages with
            | false, _ -> // We aren't permitted to send any messages, we have nothing other to do than block on the
                          // message queue.
                return! blockAndProcessNextIncomingMessage()
            | true, Queue.Cons _ -> // We're permitted to send a message and the queue is not empty.
                let! newState = tryProcessTopMessage ctx state
                return! loop newState
            | true, Queue.Nil -> // We're allowed to send a message, but the queue is empty. We have nothing to send,
                                 // thus we have nothing to do other than to block on the message queue.
                return! blockAndProcessNextIncomingMessage()
    }
    loop State.initial

let startActivity(ctx: MessageSenderContext, token: CancellationToken): Sender =
    let processor = MailboxProcessor.Start(receiver ctx, token)
    processor.Error.Add(fun ex -> ctx.Logger.Error(ex, "Error observed by the message sender mailbox"))
    processor

let setReadyToAcceptMessages(activity: Sender): bool -> unit = SetReceiveStatus >> activity.Post
let send(activity: Sender): OutgoingMessage -> unit = QueueMessage >> activity.Post
