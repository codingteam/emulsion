module Ctor.Xmpp.SharpXmppHelper

open System.Xml.Linq

open SharpXMPP
open SharpXMPP.XMPP.Client.MUC.Bookmarks.Elements
open SharpXMPP.XMPP.Client.Elements

let private bookmark (roomJid: string) (nickname: string): BookmarkedConference =
    let room = BookmarkedConference()
    room.SetAttributeValue(XName.Get("jid"), roomJid)
    let nickElement = XElement(XNamespace.Get("storage:bookmarks") + "nick", Value = nickname)
    room.Add(nickElement)
    room

let joinRoom (client: XmppClient) (roomJid: string) (nickname: string): unit =
    let room = bookmark roomJid nickname
    client.BookmarkManager.Join(room)

let message (toAddr : string) (text : string) =
    let m = XMPPMessage()
    m.SetAttributeValue(XName.Get "type", "groupchat")
    m.SetAttributeValue(XName.Get "to", toAddr)
    let body = XElement(XName.Get "body")
    body.Value <- text
    m.Add(body)
    m
