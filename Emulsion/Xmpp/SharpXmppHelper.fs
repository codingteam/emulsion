module Emulsion.Xmpp.SharpXmppHelper

open System
open System.Xml.Linq

open SharpXMPP
open SharpXMPP.XMPP
open SharpXMPP.XMPP.Client.MUC.Bookmarks.Elements
open SharpXMPP.XMPP.Client.Elements

open Emulsion

module Elements =
    let Body = XName.Get("body", Namespaces.JabberClient)
    let Delay = XName.Get("delay", "urn:xmpp:delay")
    let From = XName.Get "from"
    let Jid = XName.Get "jid"
    let Nick = XName.Get("nick", Namespaces.StorageBookmarks)
    let Stamp = XName.Get "stamp"
    let To = XName.Get "to"
    let Type = XName.Get "type"

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

let message (toAddr : string) (text : string) =
    let m = XMPPMessage()
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

let isEmptyMessage(message: XMPPMessage): bool =
    String.IsNullOrWhiteSpace message.Text

let parseMessage (message: XMPPMessage): Message =
    let nickname =
        getAttributeValue message From
        |> Option.map getResource
        |> Option.defaultValue "[UNKNOWN USER]"
    { author = nickname; text = message.Text }
