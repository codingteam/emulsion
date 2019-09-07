namespace Emulsion.Xmpp

open System.Xml.Linq

open SharpXMPP.XMPP

type Presence = {
    From: string
    States: int[]
    Error: XElement option
    Type: string option
}

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
