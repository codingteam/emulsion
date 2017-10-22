namespace Emulsion.Tests.Actors

open Akka.Actor
open Akka.TestKit.Xunit2
open Xunit

open Emulsion
open Emulsion.Actors

type CoreTests() as this =
    inherit TestKit()

    let mutable actorsCreated = 0
    let xmppActor = this.CreateTestProbe "xmpp"
    let telegramActor = this.CreateTestProbe "telegram"
    let testActorFactory _ _ name =
        actorsCreated <- actorsCreated + 1
        match name with
        | "xmpp" -> xmppActor.Ref
        | "telegram" -> telegramActor.Ref
        | _ -> failwithf "Cannot create unknown actor"
    let factories = { xmppFactory = testActorFactory
                      telegramFactory = testActorFactory }

    let spawnCore() = Core.spawn factories this.Sys "core"

    [<Fact>]
    member this.``Core actor should spawn successfully``() =
        let core = spawnCore()
        this.ExpectNoMsg()

    [<Fact>]
    member this.``Core creates one XMPP actor and one Telegram actor``() =
        let core = spawnCore()
        this.ExpectNoMsg()
        Assert.Equal(2, actorsCreated)

    [<Fact>]
    member this.``Core sends XMPP message to Telegram actor``() =
        let core = spawnCore()
        core.Tell(XmppMessage "x")
        telegramActor.ExpectMsg "x"

    [<Fact>]
    member this.``Core sends Telegram message to XMPP actor``() =
        let core = spawnCore()
        core.Tell(TelegramMessage "x")
        xmppActor.ExpectMsg "x"
