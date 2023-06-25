namespace Emulsion.Tests.Actors

open Akka.Actor
open Akka.TestKit.Xunit2
open Serilog
open Xunit
open Xunit.Abstractions

open Emulsion
open Emulsion.Actors
open Emulsion.Messaging
open Emulsion.TestFramework

type CoreTests(testOutput: ITestOutputHelper) as this =
    inherit TestKit()

    let mutable actorsCreated = 0
    let xmppActor = this.CreateTestProbe "xmpp"
    let telegramActor = this.CreateTestProbe "telegram"
    let testActorFactory _ name =
        actorsCreated <- actorsCreated + 1
        match name with
        | "xmpp" -> xmppActor.Ref
        | "telegram" -> telegramActor.Ref
        | _ -> failwithf "Cannot create unknown actor"
    let factories = { xmppFactory = testActorFactory
                      telegramFactory = testActorFactory }

    let logger = Logging.xunitLogger testOutput
    let spawnCore() = Core.spawn logger factories this.Sys (MessageArchive None) "core"

    [<Fact>]
    member this.``Core actor should spawn successfully``() =
        let _core = spawnCore()
        this.ExpectNoMsg()

    [<Fact>]
    member this.``Core creates one XMPP actor and one Telegram actor``() =
        let _core = spawnCore()
        this.ExpectNoMsg()
        Assert.Equal(2, actorsCreated)

    [<Fact>]
    member this.``Core sends XMPP message to Telegram actor``() =
        let core = spawnCore()
        core.Tell(XmppMessage (Authored { author = "xxx"; text = "x" }))
        telegramActor.ExpectMsg(OutgoingMessage (Authored { author = "xxx"; text = "x" }))

    [<Fact>]
    member this.``Core sends Telegram message to XMPP actor``() =
        let core = spawnCore()
        core.Tell(TelegramMessage(Authored { author = "Telegram user"; text = "x" }))
        xmppActor.ExpectMsg(OutgoingMessage (Authored { author = "Telegram user"; text = "x" }))
