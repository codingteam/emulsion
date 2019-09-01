namespace Emulsion.Xmpp.XmppElements

open System.Xml.Linq

type Presence = {
    From: string
    States: int[]
    Error: XElement option
    Type: string
}
