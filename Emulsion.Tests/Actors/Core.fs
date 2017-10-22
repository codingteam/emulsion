namespace Emulsion.Tests.Actors

open Akka.Actor
open Akka.TestKit.Xunit2
open Xunit

open Emulsion.Actors

type CoreTests() as this =
    inherit TestKit()

    let mutable actorsCreated = 0
    let testActorFactory _ _ name =
        actorsCreated <- actorsCreated + 1
        this.CreateTestActor(name)
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
