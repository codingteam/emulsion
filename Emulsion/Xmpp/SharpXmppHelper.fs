module Emulsion.Xmpp.SharpXmppHelper

open System.Xml.Linq

open SharpXMPP
open SharpXMPP.XMPP
open SharpXMPP.XMPP.Client.MUC.Bookmarks.Elements
open SharpXMPP.XMPP.Client.Elements

open Emulsion

module Namespaces =
    let StorageBookmarks = XNamespace.Get("storage:bookmarks")

module Elements =
    let Body = XName.Get "body"
    let Delay = XName.Get "delay"
    let From = XName.Get "from"
    let Jid = XName.Get "jid"
    let Type = XName.Get "type"
    let To = XName.Get "to"

open Namespaces
open Elements

let private bookmark (roomJid: string) (nickname: string): BookmarkedConference =
    let room = BookmarkedConference()
    room.SetAttributeValue(Jid, roomJid)
    let nickElement = XElement(StorageBookmarks + "nick", Value = nickname)
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

let parseMessage (message : XMPPMessage) : IncomingMessage =
    let nickname =
        getAttributeValue message From
        |> Option.map getResource
        |> Option.defaultValue "[UNKNOWN USER]"
    XmppMessage { author = nickname; text = message.Text }
