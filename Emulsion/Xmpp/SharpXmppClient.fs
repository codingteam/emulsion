module Emulsion.Xmpp.SharpXmppClient

open System

open JetBrains.Lifetimes
open Serilog
open SharpXMPP
open SharpXMPP.XMPP.Client.Elements

open Emulsion
open Emulsion.Lifetimes
open Emulsion.Xmpp
open Emulsion.Xmpp.XmppClient
open SharpXMPP.XMPP

type RoomInfo = {
    RoomJid: JID
    Nickname: string
}

type MessageInfo = {
    RecipientJid: JID
    Text: string
}

type MessageDeliveryInfo = {
    MessageId: string

    /// Resolves after the message is guaranteed to be delivered to the recipient.
    Delivery: Async<unit>
}

/// Establish a connection to the server and log in. Returns a connection lifetime that will terminate if the connection
/// terminates.
let connect (logger: ILogger) (client: IXmppClient): Async<Lifetime> = async {
    let connectionLifetime = new LifetimeDefinition()
    client.AddConnectionFailedHandler connectionLifetime.Lifetime <| fun error ->
        logger.Error(error.Exception, "Connection failed: {Message}", error.Message)
        connectionLifetime.Terminate()

    do! client.Connect()
    return connectionLifetime.Lifetime
}

let private isSelfPresence (roomInfo: RoomInfo) (presence: XMPPPresence) =
    let presence = SharpXmppHelper.parsePresence presence
    let expectedJid = sprintf "%s/%s" roomInfo.RoomJid.BareJid roomInfo.Nickname
    presence.Type = None && presence.From = expectedJid && Array.contains 110 presence.States

let private isLeavePresence (roomInfo: RoomInfo) (presence: XMPPPresence) =
    let presence = SharpXmppHelper.parsePresence presence
    let expectedJid = sprintf "%s/%s" roomInfo.RoomJid.BareJid roomInfo.Nickname
    presence.From = expectedJid && Array.contains 110 presence.States && presence.Type = Some "unavailable"

let private extractException (roomInfo: RoomInfo) (presence: XMPPPresence) =
    let presence = SharpXmppHelper.parsePresence presence
    let expectedJid = sprintf "%s/%s" roomInfo.RoomJid.BareJid roomInfo.Nickname
    if presence.From = expectedJid then
        presence.Error
        |> Option.map (fun e -> Exception(sprintf "Error: %A" e))
    else None

let private addMessageHandler (lifetime: Lifetime) (client: XmppClient) handler =
    let handlerDelegate = XmppConnection.MessageHandler handler
    client.add_Message handlerDelegate
    lifetime.OnTermination(fun () -> client.remove_Message handlerDelegate) |> ignore

/// Enter the room, returning the in-room lifetime. Will terminate if kicked or left the room.
let enterRoom (client: IXmppClient) (lifetime: Lifetime) (roomInfo: RoomInfo): Async<Lifetime> = async {
    use connectionLifetimeDefinition = lifetime.CreateNested()
    let connectionLifetime = connectionLifetimeDefinition.Lifetime

    let roomLifetimeDefinition = lifetime.CreateNested()
    let roomLifetime = roomLifetimeDefinition.Lifetime

    let tcs = nestedTaskCompletionSource connectionLifetime

    // Success and error handlers:
    client.AddPresenceHandler connectionLifetime (fun presence ->
        if isSelfPresence roomInfo presence
        then tcs.SetResult()
        else
            match extractException roomInfo presence with
            | Some ex -> tcs.SetException ex
            | None -> ()
    )

    // Room leave handler:
    client.AddPresenceHandler roomLifetime (fun presence ->
        if isLeavePresence roomInfo presence
        then roomLifetimeDefinition.Terminate()
    )

    try
        // Start the join process, wait for a result:
        client.JoinMultiUserChat roomInfo.RoomJid roomInfo.Nickname
        do! Async.AwaitTask tcs.Task
        return roomLifetime
    with
    | ex ->
        // In case of an error, terminate the room lifetime (but leave it intact in case of success):
        roomLifetimeDefinition.Terminate()
        return ExceptionUtils.reraise ex
}

let private hasMessageId messageId message =
    SharpXmppHelper.getMessageId message = Some messageId

let private awaitMessageReceival (lifetime: Lifetime) client messageId = async {
    use messageLifetimeDefinition = lifetime.CreateNested()
    let messageLifetime = messageLifetimeDefinition.Lifetime
    let messageReceivedTask = nestedTaskCompletionSource messageLifetime
    addMessageHandler lifetime client (fun _ message ->
        if hasMessageId messageId message then
            messageReceivedTask.SetResult()
    )

    do! Async.AwaitTask messageReceivedTask.Task
}

/// Sends the message to the room. Returns an object that allows to track the message receival.
/// TODO[F]: Write tests for this function.
let sendRoomMessage (lifetime: Lifetime) (client: XmppClient) (messageInfo: MessageInfo): Async<MessageDeliveryInfo> =
    async {
        let messageId = Guid.NewGuid().ToString() // TODO[F]: Move to a new function
        let message = SharpXmppHelper.message (Some messageId) messageInfo.RecipientJid.FullJid messageInfo.Text
        let! delivery = Async.StartChild <| awaitMessageReceival lifetime client messageId
        client.Send message
        return {
            MessageId = messageId
            Delivery = delivery
        }
    }

/// Waits for the message to be delivered.
/// TODO[F]: Write tests for this function.
let awaitMessageDelivery (deliveryInfo: MessageDeliveryInfo): Async<unit> =
    deliveryInfo.Delivery
