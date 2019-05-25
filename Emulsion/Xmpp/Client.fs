namespace Emulsion.Xmpp

open Akka.Actor
open SharpXMPP

open Emulsion.Settings

type Client =
    { construct : IActorRef -> XmppClient
      run : XmppClient -> unit
      send : XmppClient -> string -> unit }

    with
        static member private create settings (core : IActorRef) =
            XmppClient.create settings core.Tell

        static member sharpXmpp (settings : XmppSettings) : Client =
            { construct = Client.create settings
              run = XmppClient.run
              send = XmppClient.send settings }
