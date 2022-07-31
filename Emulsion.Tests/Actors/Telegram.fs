namespace Emulsion.Tests.Actors

open Akka.TestKit.Xunit2
open Xunit
open Xunit.Abstractions

open Emulsion
open Emulsion.Actors
open Emulsion.Messaging
open Emulsion.Messaging.MessageSystem
open Emulsion.Tests.TestUtils

type TelegramTest(testOutput: ITestOutputHelper) =
    inherit TestKit()

    [<Fact>]
    member this.``Telegram actor should pass an outgoing message to the Telegram client``() =
        let mutable sentMessage = None
        let telegram = {
            new IMessageSystem with
                member _.RunSynchronously _ = ()
                member _.PutMessage message =
                    sentMessage <- Some message
        }
        let actor = Telegram.spawn (Logging.xunitLogger testOutput) telegram this.Sys "telegram"
        let msg = OutgoingMessage (Authored { author = "x"; text = "message" })
        actor.Tell(msg, this.TestActor)
        this.ExpectNoMsg()
        Assert.Equal(Some msg, sentMessage)
