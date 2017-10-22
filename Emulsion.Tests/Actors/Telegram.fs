namespace Emulsion.Tests.Actors

open Akka.TestKit.Xunit2
open Xunit

open Emulsion
open Emulsion.Actors
open Emulsion.Tests

type TelegramTest() =
    inherit TestKit()

    [<Fact>]
    member this.``Telegram should send incoming string message to the core actor``() =
        let sentMessage = ref ""
        let telegram : Telegram.TelegramModule =
            { run = fun _ _ -> ()
              send = fun _ msg -> sentMessage := msg }
        let actor = Telegram.spawn Settings.testConfiguration.telegram telegram this.Sys this.TestActor "telegram"
        actor.Tell("message", this.TestActor)
        this.ExpectNoMsg()
        Assert.Equal("message", !sentMessage)
