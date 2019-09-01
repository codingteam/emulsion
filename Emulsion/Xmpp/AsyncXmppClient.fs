module Emulsion.Xmpp.AsyncXmppClient

open System.Security

open Emulsion.Lifetimes

type ServerInfo = {
    Host: string
    Port: uint16
}

type SignInInfo = {
    Login: string
    Password: string
}

type Jid = string

type RoomInfo = {
    RoomJid: Jid
    Nickname: string
}

type MessageInfo = {
    RecipientJid: Jid
    Text: string
}

type MessageDeliveryInfo = Async<unit> // Resolves after the message is guaranteed to be delivered to the recipient.

type IAsyncXmppClient =
    // TODO[F]: Implement the remaining functions in SharpXmppClient

    /// Enter the room, returning the in-room lifetime. Will terminate if kicked or left the room.
    abstract member EnterRoom : RoomInfo -> Async<Lifetime>

    /// Sends the message to the room.
    abstract member SendMessage : MessageInfo -> Async<MessageDeliveryInfo>

    /// Waits for the message to be delivered.
    abstract member AwaitMessageDelivery : MessageDeliveryInfo -> Async<unit>

    /// Disconnects from the server (if connected) and frees all the resources associated with the client.
    abstract member DisposeAsync : unit -> Async<unit>
