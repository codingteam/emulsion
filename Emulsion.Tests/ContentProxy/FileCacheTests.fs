namespace Emulsion.Tests.ContentProxy

open System
open System.Collections.Generic
open System.IO
open System.Security.Cryptography
open System.Threading.Tasks

open Xunit
open Xunit.Abstractions

open Emulsion.ContentProxy
open Emulsion.Settings
open Emulsion.TestFramework
open Emulsion.TestFramework.Logging

type FileCacheTests(outputHelper: ITestOutputHelper) =

    let sha256 = SHA256.Create()

    let cacheDirectory = lazy (
        let path = Path.GetTempFileName()
        File.Delete path
        Directory.CreateDirectory path |> ignore
        path
    )

    let setUpFileCache(totalLimitBytes: uint64) =
        let settings = {
            Directory = cacheDirectory.Value
            FileSizeLimitBytes = 1048576UL
            TotalCacheSizeLimitBytes = totalLimitBytes
        }

        new FileCache(xunitLogger outputHelper, settings, sha256)

    let assertCacheState(entries: (string * byte[]) seq) =
        let files =
            Directory.EnumerateFileSystemEntries(cacheDirectory.Value)
            |> Seq.map(fun file ->
                let name = Path.GetFileName file
                let content = File.ReadAllBytes file
                name, content
            )
            |> Map.ofSeq

        let entries =
            entries
            |> Seq.map(fun (k, v) -> FileCache.FileName(sha256, k), v)
            |> Map.ofSeq

        Assert.Equal<IEnumerable<_>>(entries, files)

    [<Fact>]
    member _.``File should be cached``(): unit =
        Assert.False true

    [<Fact>]
    member _.``Too big file should be proxied``(): unit =
        Assert.False true

    [<Fact>]
    member _.``Cleanup should be triggered``(): Task = task {
        use fileCache = setUpFileCache 129UL
        use fileStorage = new WebFileStorage(Map.ofArray [|
            "a", [| for _ in 1 .. 128 do yield 1uy |]
            "b", [| for _ in 1 .. 128 do yield 2uy |]
            "c", [| 3uy |]
        |])

        let! file = fileCache.Download(fileStorage.Link("a"), "a", 128UL)
        Assert.True file.IsSome
        assertCacheState [| "a", fileStorage.Content("a") |]

        let! file = fileCache.Download(fileStorage.Link("b"), "b", 128UL)
        Assert.True file.IsSome
        assertCacheState [| "b", fileStorage.Content("b") |]

        let! file = fileCache.Download(fileStorage.Link("c"), "c", 1UL)
        Assert.True file.IsSome
        assertCacheState [|
            "b", fileStorage.Content("b")
            "c", fileStorage.Content("c")
        |]
    }

    [<Fact>]
    member _.``File should be read even after cleanup``(): unit =
        Assert.False true

    [<Fact>]
    member _.``File should be re-downloaded after cleanup even if there's a outdated read session in progress``(): unit =
        Assert.False true

    interface IDisposable with
        member _.Dispose() = sha256.Dispose()
