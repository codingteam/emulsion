module Emulsion.MessageSender

open System

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

let activity(ctx: MessageSenderContext): MailboxProcessor<OutgoingMessage> = MailboxProcessor.Start(fun inbox ->
    let rec loop() = async {
        let! msg = inbox.Receive()
        do! sendRetryLoop ctx msg
        return! loop()
    }
    loop()
)

let send(activity: MailboxProcessor<OutgoingMessage>): OutgoingMessage -> unit = activity.Post
