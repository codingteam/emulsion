module Emulsion.MessageSender

open System
open System.Threading

open Serilog

type MessageSenderContext = {
    Send: OutgoingMessage -> Async<unit>
    Logger: ILogger
    RestartCooldown: TimeSpan
}

let rec private sendRetryLoop ctx msg = async {
    try
        do! ctx.Send msg
    with
    | ex ->
        ctx.Logger.Error(ex, "Error when trying to send message {Message}", msg)
        ctx.Logger.Information("Waiting for {RestartCooldown} to resume processing output message queue",
                               ctx.RestartCooldown)
        do! Async.Sleep(int ctx.RestartCooldown.TotalMilliseconds)
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
