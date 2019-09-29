namespace Emulsion.Tests.Actors

open Akka.TestKit.Xunit2
open Xunit
open Xunit.Abstractions

open Emulsion
open Emulsion.Actors
open Emulsion.MessageSystem
open Emulsion.Tests.TestUtils

type XmppTest(testOutput: ITestOutputHelper) =
    inherit TestKit()

    [<Fact>]
    member this.``XMPP actor should pass an outgoing message to the XMPP module``() =
        let mutable sentMessage = None
        let xmpp = {
            new IMessageSystem with
                member _.RunSynchronously _ = ()
                member _.PutMessage message =
                    sentMessage <- Some message
        }
        let actor = Xmpp.spawn (Logging.xunitLogger testOutput) xmpp this.Sys "xmpp"
        let message = OutgoingMessage { author = "@nickname"; text = "message" }
        actor.Tell(message, this.TestActor)
        this.ExpectNoMsg()
        Assert.Equal(Some message, sentMessage)
