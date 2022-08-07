namespace Emulsion.Tests.ContentProxy

open Xunit
open Xunit.Abstractions

type FileCacheTests(outputHelper: ITestOutputHelper) =
    member _.``File should be cached``(): unit =
        Assert.False true

    member _.``Too big file should be proxied``(): unit =
        Assert.False true

    member _.``Cleanup should be triggered``(): unit =
        Assert.False true

    member _.``File should be read even after cleanup``(): unit =
        Assert.False true

    member _.``File should be re-downloaded after cleanup even if there's a outdated read session in progress``(): unit =
        Assert.False true
