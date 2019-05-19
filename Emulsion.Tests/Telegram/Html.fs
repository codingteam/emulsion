module Emulsion.Tests.Telegram.Html

open Xunit

open Emulsion.Telegram

[<Fact>]
let ``Html should escape properly``() =
    Assert.Equal("&lt;html&gt;&amp;&lt;css&gt;", Html.escape "<html>&<css>")
    Assert.Equal("&lt;script&gt;alert('XSS')&lt;/script&gt;", Html.escape "<script>alert('XSS')</script>")
    Assert.Equal("noescape", Html.escape "noescape")
