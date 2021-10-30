module Emulsion.Tests.ContentProxy.ProxyTests

open System

open Xunit

open Emulsion.ContentProxy

let private salt = "mySalt"

let private doTest number =
    let encoded = Proxy.encodeHashId salt number
    let decoded = Proxy.decodeHashId salt encoded

    Assert.Equal(number, decoded)

[<Fact>]
let ``decode + encode should round-trip correctly``(): unit = doTest 123L

[<Fact>]
let ``zero number round-trip``(): unit = doTest 0L

[<Fact>]
let ``long number round-trip``(): unit = doTest 21474836470L

[<Fact>]
let ``Int64.MaxValue round-trip``(): unit = doTest Int64.MaxValue
