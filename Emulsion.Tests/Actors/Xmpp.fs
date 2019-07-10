namespace Emulsion.Tests.Actors

open Akka.TestKit.Xunit2
open Xunit

open Emulsion
open Emulsion.Actors
open Emulsion.MessageSystem

type XmppTest() =
    inherit TestKit()

    [<Fact>]
    member this.``XMPP actor should pass an outgoing message to the XMPP module``() =
        let mutable sentMessage = None
        let xmpp = {
            new IMessageSystem with
                member __.Run _ = ()
                member __.PutMessage message =
                    sentMessage <- Some message
        }
        let actor = Xmpp.spawn xmpp this.Sys "xmpp"
        let message = OutgoingMessage { author = "@nickname"; text = "message" }
        actor.Tell(message, this.TestActor)
        this.ExpectNoMsg()
        Assert.Equal(Some message, sentMessage)
