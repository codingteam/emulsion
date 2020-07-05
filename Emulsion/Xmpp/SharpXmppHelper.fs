/// Helper functions to deal with SharpXMPP low-level details (such as XML stuff).
module Emulsion.Xmpp.SharpXmppHelper

open System
open System.Xml.Linq

open SharpXMPP
open SharpXMPP.XMPP
open SharpXMPP.XMPP.Client.MUC.Bookmarks.Elements
open SharpXMPP.XMPP.Client.Elements

open Emulsion
open Emulsion.Xmpp

module Namespaces =
    let MucUser = "http://jabber.org/protocol/muc#user"

module Attributes =
    let Code = XName.Get "code"
    let From = XName.Get "from"
    let Id = XName.Get "id"
    let Jid = XName.Get "jid"
    let Stamp = XName.Get "stamp"
    let To = XName.Get "to"
    let Type = XName.Get "type"

open Attributes

module Elements =
    let Body = XName.Get("body", Namespaces.JabberClient)
    let Delay = XName.Get("delay", "urn:xmpp:delay")
    let Error = XName.Get("error", Namespaces.JabberClient)
    let Nick = XName.Get("nick", Namespaces.StorageBookmarks)
    let Status = XName.Get("status", Namespaces.MucUser)
    let X = XName.Get("x", Namespaces.MucUser)

open Elements

let private bookmark (roomJid: string) (nickname: string): BookmarkedConference =
    let room = BookmarkedConference()
    room.SetAttributeValue(Jid, roomJid)
    let nickElement = XElement(Nick, Value = nickname)
    room.Add(nickElement)
    room

let joinRoom (client: XmppClient) (roomJid: string) (nickname: string): unit =
    let room = bookmark roomJid nickname
    client.BookmarkManager.Join(room)

let message (id: string) (toAddr: string) (text: string): XMPPMessage =
    let m = XMPPMessage()
    m.SetAttributeValue(Id, id)
    m.SetAttributeValue(Type, "groupchat")
    m.SetAttributeValue(To, toAddr)
    let body = XElement(Body)
    body.Value <- text
    m.Add(body)
    m

let private getAttributeValue (element : XElement) attributeName =
    let attribute = element.Attribute(attributeName)
    if isNull attribute
    then None
    else Some attribute.Value

let private getResource jidText = JID(jidText).Resource

let isOwnMessage (nickname : string) (message : XMPPMessage) : bool =
    getAttributeValue message From
    |> Option.map getResource
    |> Option.map(fun resource -> resource = nickname)
    |> Option.defaultValue false

let isHistoricalMessage (message : XMPPMessage) : bool =
    not (
        message.Elements Delay
        |> Seq.isEmpty
    )

let isGroupChatMessage(message: XMPPMessage): bool =
    let messageType = getAttributeValue message Type
    messageType = Some "groupchat"

let isEmptyMessage(message: XMPPMessage): bool =
    String.IsNullOrWhiteSpace message.Text

/// See https://xmpp.org/registrar/mucstatus.html
let private removalCodes = Set.ofArray [| 301; 307; 321; 322; 332 |]
let hasRemovalCode(states: int[]): bool =
    states |> Array.exists (fun x -> Set.contains x removalCodes)

let getMessageId(message: XMPPMessage): string option =
    getAttributeValue message Id

let getMessageError(message: XMPPMessage): XElement option =
    message.Element Error |> Option.ofObj

let parseMessage (message: XMPPMessage): Message =
    let nickname =
        getAttributeValue message From
        |> Option.map getResource
        |> Option.defaultValue "[UNKNOWN USER]"
    Authored { author = nickname; text = message.Text }

let parsePresence(presence: XMPPPresence): Presence =
    let from = getAttributeValue presence From |> Option.defaultValue ""
    let presenceType = getAttributeValue presence Type
    let states =
        presence.Element X
        |> Option.ofObj
        |> Option.map (fun x ->
            x.Elements Status
            |> Seq.choose (fun s -> getAttributeValue s Code)
            |> Seq.map int
        )
        |> Option.map Seq.toArray
        |> Option.defaultWith(fun () -> Array.empty)
    let error = presence.Element Error |> Option.ofObj
    { From = from; Type = presenceType; States = states; Error = error }
