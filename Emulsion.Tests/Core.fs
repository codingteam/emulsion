namespace Emulsion.Tests

open Akka.TestKit.Xunit2
open Xunit

open Emulsion

type CoreTests() =
    inherit TestKit()

    [<Fact>]
    member this.``Core actor should spawn successfully``() =
        let core = Core.spawn this.Sys
        this.ExpectNoMsg()
