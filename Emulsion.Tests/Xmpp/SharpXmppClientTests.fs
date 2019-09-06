namespace Emulsion.Tests.Xmpp

open SharpXMPP
open Xunit
open Xunit.Abstractions

open Emulsion.Tests.TestUtils
open Emulsion.Xmpp

type SharpXmppClientTests(testOutput: ITestOutputHelper) =
    let logger = Logging.xunitLogger testOutput

    [<Fact>]
    member __.``connect function calls the Connect method of the client passed``(): unit =
        let mutable connectCalled = false
        let client = XmppClientFactory.create(fun () -> async { connectCalled <- true })
        Async.RunSynchronously <| SharpXmppClient.connect logger client |> ignore
        Assert.True connectCalled

    [<Fact>]
    member __.``connect function returns a lifetime terminated whenever the ConnectionFailed callback is triggered``()
        : unit =
            let mutable callback = ignore
            let client = XmppClientFactory.create(addConnectionFailedHandler = fun _ h -> callback <- h)
            let lt = Async.RunSynchronously <| SharpXmppClient.connect logger client
            Assert.True lt.IsAlive
            callback(ConnFailedArgs())
            Assert.False lt.IsAlive
