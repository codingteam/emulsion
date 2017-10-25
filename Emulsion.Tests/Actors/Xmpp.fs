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
    member this.``XMPP should send incoming string message to the Core actor``() =
        let sentMessage = ref ""
        let xmppSettings = Settings.testConfiguration.xmpp
        let xmpp : XmppModule =
            { construct = (xmppModule xmppSettings).construct
              run = fun _ -> ()
              send = fun _ msg -> sentMessage := msg }
        let actor = Xmpp.spawn xmpp this.Sys this.TestActor "xmpp"
        actor.Tell("message", this.TestActor)
        this.ExpectNoMsg()
        Assert.Equal("message", !sentMessage)
