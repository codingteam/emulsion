module Emulsion.MessageSender

open System
open System.Threading

type MessageSenderContext = {
    send: OutgoingMessage -> Async<unit>
    logError: Exception -> unit
    cooldown: TimeSpan
}

let rec private sendRetryLoop ctx msg = async {
    try
        do! ctx.send msg
    with
    | ex ->
        ctx.logError ex
        do! Async.Sleep(int ctx.cooldown.TotalMilliseconds)
        return! sendRetryLoop ctx msg
}

type Sender = MailboxProcessor<OutgoingMessage>
let private receiver ctx (inbox: Sender) =
    let rec loop() = async {
        let! msg = inbox.Receive()
        do! sendRetryLoop ctx msg
        return! loop()
    }
    loop()

let startActivity(ctx: MessageSenderContext, token: CancellationToken): Sender =
    MailboxProcessor.Start(receiver ctx, token)

let send(activity: Sender): OutgoingMessage -> unit = activity.Post
