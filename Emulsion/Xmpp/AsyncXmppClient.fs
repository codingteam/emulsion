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

type RoomInfo = {
    RoomJid: string
    Nickname: string
}

type MessageInfo = {
    RecipientJid: string
    Text: string
}

type Lifetime = CancellationToken // TODO[F]: Determine a proper lifetime?

type IAsyncXmppClient =
    abstract member Connect : ServerInfo -> Async<unit>
    abstract member SignIn : SignInInfo -> Async<unit>
    abstract member EnterRoom : RoomInfo -> Async<Lifetime>
    abstract member SendMessage : Lifetime -> MessageInfo -> Async<unit>
    abstract member DisposeAsync : unit -> Async<unit>
