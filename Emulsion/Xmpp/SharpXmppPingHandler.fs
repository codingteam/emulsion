﻿namespace Emulsion.Xmpp

open SharpXMPP.XMPP.Client

type SharpXmppPingHandler() =
    inherit PayloadHandler()

    override _.Handle(connection, element) =
        if SharpXmppHelper.isPing element then
            connection.Send(element.Reply())
            true
        else
            false
