namespace Emulsion.Tests.Actors

open Akka.TestKit.Xunit2
open Xunit

open Emulsion
open Emulsion.Actors
open Emulsion.Xmpp
open Emulsion.Tests

type XmppTest() =
    inherit TestKit()

    [<Fact>]
    member this.``XMPP actor should pass an outgoing message to the XMPP module``() =
        let sentMessage = ref ""
        let xmppSettings = Settings.testConfiguration.xmpp
        let xmpp : XmppModule =
            { construct = (xmppModule xmppSettings).construct
              run = fun _ -> ()
              send = fun _ msg -> sentMessage := msg }
        let actor = Xmpp.spawn xmpp this.Sys this.TestActor "xmpp"
        actor.Tell(OutgoingMessage { author = "@nickname"; text = "message" }, this.TestActor)
        this.ExpectNoMsg()
        Assert.Equal("<@nickname> message", !sentMessage)
