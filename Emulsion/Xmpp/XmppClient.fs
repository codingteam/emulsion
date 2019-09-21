/// A general abstraction around an XMPP client and common functions.
module Emulsion.Xmpp.XmppClient

open System

open JetBrains.Lifetimes

open SharpXMPP
open SharpXMPP.XMPP
open SharpXMPP.XMPP.Client.Elements

open Emulsion
open Emulsion.Lifetimes
open Emulsion.Xmpp

type IXmppClient =
    abstract member Connect: unit -> Async<unit>
    abstract member JoinMultiUserChat: roomJid: JID -> nickname: string -> unit
    abstract member Send: XMPPMessage -> unit
    abstract member AddConnectionFailedHandler: Lifetime -> (ConnFailedArgs -> unit) -> unit
    abstract member AddSignedInHandler: Lifetime -> (SignedInArgs -> unit) -> unit
    abstract member AddElementHandler: Lifetime -> (ElementArgs -> unit) -> unit
    abstract member AddPresenceHandler: Lifetime -> (XMPPPresence -> unit) -> unit
    abstract member AddMessageHandler: Lifetime -> (XMPPMessage -> unit) -> unit

/// Establish a connection to the server and log in. Returns a connection lifetime that will terminate if the connection
/// terminates.
let connect (client: IXmppClient): Async<Lifetime> = async {
    let connectionLifetime = new LifetimeDefinition()
    client.AddConnectionFailedHandler connectionLifetime.Lifetime <| fun _ ->
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
    presence.From = expectedJid && presence.Type = Some "unavailable" && SharpXmppHelper.hasRemovalCode presence.States

let private extractPresenceException (roomInfo: RoomInfo) (presence: XMPPPresence) =
    let presence = SharpXmppHelper.parsePresence presence
    let expectedJid = sprintf "%s/%s" roomInfo.RoomJid.BareJid roomInfo.Nickname
    if presence.From = expectedJid then
        presence.Error
        |> Option.map (fun e -> Exception(sprintf "Error: %A" e))
    else None

/// Enter the room, returning the in-room lifetime. Will terminate if kicked or left the room.
let enterRoom (client: IXmppClient) (lifetime: Lifetime) (roomInfo: RoomInfo): Async<Lifetime> = async {
    use connectionLifetimeDefinition = lifetime.CreateNested()
    let connectionLifetime = connectionLifetimeDefinition.Lifetime

    let roomLifetimeDefinition = lifetime.CreateNested()
    let roomLifetime = roomLifetimeDefinition.Lifetime

    let tcs = connectionLifetime.CreateTaskCompletionSource()

    // Success and error handlers:
    client.AddPresenceHandler connectionLifetime (fun presence ->
        if isSelfPresence roomInfo presence
        then tcs.SetResult()
        else
            match extractPresenceException roomInfo presence with
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

let private extractMessageException message =
    SharpXmppHelper.getMessageError message
    |> Option.map(fun e -> Exception(sprintf "Error: %A" e))

let private awaitMessageReceival (client: IXmppClient) (lifetime: Lifetime) messageId =
    // We need to perform this part synchronously to avoid the race condition between adding a message handler and
    // actually sending a message.
    let messageLifetimeDefinition = lifetime.CreateNested()
    let messageLifetime = messageLifetimeDefinition.Lifetime
    let messageReceivedTask = messageLifetime.CreateTaskCompletionSource()
    client.AddMessageHandler lifetime (fun message ->
        if hasMessageId messageId message then
            match extractMessageException message with
            | Some ex -> messageReceivedTask.SetException ex
            | None -> messageReceivedTask.SetResult()
    )
    async {
        try
            do! Async.AwaitTask messageReceivedTask.Task
        finally
            messageLifetimeDefinition.Dispose()
    }

let private newMessageId(): string =
    Guid.NewGuid().ToString()

/// Sends the message to the room. Returns an object that allows to track the message receival.
let sendRoomMessage (client: IXmppClient) (lifetime: Lifetime) (messageInfo: MessageInfo): Async<MessageDeliveryInfo> =
    async {
        let messageId = newMessageId()
        let message = SharpXmppHelper.message messageId messageInfo.RecipientJid.FullJid messageInfo.Text
        let! delivery = Async.StartChild <| awaitMessageReceival client lifetime messageId
        client.Send message
        return {
            MessageId = messageId
            Delivery = delivery
        }
    }

/// Waits for the message to be delivered.
let awaitMessageDelivery (deliveryInfo: MessageDeliveryInfo): Async<unit> =
    deliveryInfo.Delivery
