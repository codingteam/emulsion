module Emulsion.Xmpp.SharpXmppHelper

open System.Xml.Linq

open SharpXMPP
open SharpXMPP.XMPP
open SharpXMPP.XMPP.Client.MUC.Bookmarks.Elements
open SharpXMPP.XMPP.Client.Elements

open Emulsion

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

let private getAttributeValue (element : XElement) attributeName =
    let attribute = element.Attribute(XName.Get attributeName)
    if isNull attribute
    then None
    else Some attribute.Value

let private getResource jidText = JID(jidText).Resource

let parseMessage (message : XMPPMessage) : IncomingMessage option =
    getAttributeValue message "from"
    |> Option.map getResource
    |> Option.map (fun nickname -> XmppMessage(nickname, message.Text))
