namespace Emulsion.Tests.Actors

open Akka.TestKit.Xunit2
open Xunit

open Emulsion
open Emulsion.Actors
open Emulsion.Tests

type TelegramTest() =
    inherit TestKit()

    [<Fact>]
    member this.``Telegram actor should pass an outgoing message to the Telegram module``() =
        let sentMessage = ref None
        let telegram : Telegram.TelegramModule =
            { run = fun _ _ -> ()
              send = fun _ msg -> sentMessage := Some msg }
        let actor = Telegram.spawn Settings.testConfiguration.telegram telegram this.Sys this.TestActor "telegram"
        let msg = OutgoingMessage { author = "x"; text = "message" }
        actor.Tell(msg, this.TestActor)
        this.ExpectNoMsg()
        Assert.Equal(Some msg, !sentMessage)
