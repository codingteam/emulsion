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
    let testActorFactory _ name =
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
        core.Tell(XmppMessage { author = "xxx"; text = "x" })
        telegramActor.ExpectMsg(OutgoingMessage { author = "xxx"; text = "x" })

    [<Fact>]
    member this.``Core sends Telegram message to XMPP actor``() =
        let core = spawnCore()
        core.Tell(TelegramMessage { main = { author = "Telegram user"; text = "x" }; replyTo = None })
        xmppActor.ExpectMsg(OutgoingMessage { author = "Telegram user"; text = "x" })
