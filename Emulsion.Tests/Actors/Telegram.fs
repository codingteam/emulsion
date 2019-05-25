namespace Emulsion.Tests.Actors

open Akka.TestKit.Xunit2
open Xunit

open Emulsion
open Emulsion.Actors
open Emulsion.Telegram

type TelegramTest() =
    inherit TestKit()

    [<Fact>]
    member this.``Telegram actor should pass an outgoing message to the Telegram module``() =
        let mutable sentMessage = None
        let telegram : Client =
            { run = fun _ -> ()
              send = fun msg -> sentMessage <- Some msg }
        let actor = Telegram.spawn telegram this.Sys this.TestActor "telegram"
        let msg = OutgoingMessage { author = "x"; text = "message" }
        actor.Tell(msg, this.TestActor)
        this.ExpectNoMsg()
        Assert.Equal(Some msg, sentMessage)
