module Emulsion.Xmpp.AsyncXmppClient

open System.Security
open System.Threading

type ServerInfo = {
    Host: string
    Port: uint16
}

type SignInInfo = {
    Login: string
    Password: SecureString
}

type Jid = string

type RoomInfo = {
    RoomJid: Jid
    Nickname: string
}

type MessageInfo = {
    RecipientJid: string
    Text: string
}

type MessageDeliveryInfo = Async<unit> // Resolves after the message is guaranteed to be delivered to the recipient.

type Lifetime = CancellationToken // TODO[F]: Determine a proper lifetime?

type IAsyncXmppClient =
    /// Establish a connection to the server. Returns a connection lifetime that will terminate if the connection
    /// terminates.
    abstract member Connect : ServerInfo -> Async<Lifetime>

    /// Sign in with the provided credentials. Returns a session lifetime that will terminate if the session terminates.
    abstract member SignIn : SignInInfo -> Async<Lifetime>

    /// Enter the room, returning the in-room lifetime. Will terminate if kicked or left the room.
    abstract member EnterRoom : RoomInfo -> Async<Lifetime>

    /// Sends the message to the room.
    abstract member SendMessage : MessageInfo -> Async<MessageDeliveryInfo>

    /// Waits for the message to be delivered.
    abstract member AwaitMessageDelivery : MessageDeliveryInfo -> Async<unit>

    /// Disconnects from the server (if connected) and frees all the resources associated with the client.
    abstract member DisposeAsync : unit -> Async<unit>
