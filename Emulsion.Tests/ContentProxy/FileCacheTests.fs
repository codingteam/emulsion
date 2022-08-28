namespace Emulsion.Tests.ContentProxy

open System
open System.Collections.Generic
open System.IO
open System.Security.Cryptography
open System.Threading.Tasks

open JetBrains.Lifetimes
open Xunit
open Xunit.Abstractions

open Emulsion.ContentProxy
open Emulsion.TestFramework

type FileCacheTests(output: ITestOutputHelper) =

    let sha256 = SHA256.Create()

    let cacheDirectory = lazy FileCacheUtil.newCacheDirectory()

    let setUpFileCache(totalLimitBytes: uint64) =
        FileCacheUtil.setUpFileCache output sha256 cacheDirectory.Value totalLimitBytes

    let assertCacheState(entries: (string * byte[]) seq) =
        let files =
            Directory.EnumerateFileSystemEntries(cacheDirectory.Value)
            |> Seq.filter(fun f ->
                if FileCache.IsMoveAndDeleteModeEnabled then not(f.EndsWith ".deleted")
                else true
            )
            |> Seq.map(fun file ->
                let name = Path.GetFileName file
                let content = File.ReadAllBytes file
                name, content
            )
            |> Map.ofSeq

        let entries =
            entries
            |> Seq.map(fun (k, v) -> FileCache.EncodeFileName(sha256, k), v)
            |> Map.ofSeq

        Assert.Equal<IEnumerable<_>>(entries.Keys, files.Keys)
        for key in entries.Keys do
            Assert.Equal<IEnumerable<_>>(entries[key], files[key])

    let assertFileDownloaded (fileCache: FileCache) (fileStorage: WebFileStorage) entry size = async {
        let! file = fileCache.Download(fileStorage.Link entry, entry, size)
        Assert.True file.IsSome
    }

    let assertCacheValidationError setUpAction expectedMessage =
        use fileCache = setUpFileCache 1UL
        use fileStorage = new WebFileStorage(Map.empty)

        setUpAction()

        Lifetime.Using(fun lt ->
            let mutable error: Exception option = None
            fileCache.Error.Advise(lt, fun e -> error <- Some e)

            let file = Async.RunSynchronously <| fileCache.Download(fileStorage.Link "nonexistent", "nonexistent", 1UL)
            Assert.True file.IsNone

            Assert.True error.IsSome
            Assert.Equal(expectedMessage, error.Value.Message)
        )

    let readAllBytes =
        StreamUtils.readAllBytes (Logging.xunitLogger output)

    interface IDisposable with
        member _.Dispose() = sha256.Dispose()

    [<Fact>]
    member _.``File cache should throw a validation exception if the cache directory contains directories``(): unit =
        assertCacheValidationError
            (fun() -> Directory.CreateDirectory(Path.Combine(cacheDirectory.Value, "aaa")) |> ignore)
            "Cache directory invalid: contains a subdirectory \"aaa\"."

    [<Fact>]
    member _.``File cache should throw a validation exception if the cache directory contains non-conventionally-named files``(): unit =
        assertCacheValidationError
            (fun() -> File.Create(Path.Combine(cacheDirectory.Value, "aaa.txt")).Dispose())
            ("Cache directory invalid: contains an entry \"aaa.txt\" which doesn't correspond to a base58-encoded " +
             "SHA-256 hash.")

    [<Fact>]
    member _.``File should be cached``(): Task = task {
        use fileCache = setUpFileCache 1024UL
        use fileStorage = new WebFileStorage(Map.ofArray [|
            "a", [| for _ in 1 .. 5 do yield 1uy |]
        |])

        do! assertFileDownloaded fileCache fileStorage "a" 5UL
        assertCacheState [| "a", fileStorage.Content("a") |]
    }

    [<Fact>]
    member _.``Too big file should be proxied``(): Task = task {
        use fileCache = setUpFileCache 1UL
        use fileStorage = new WebFileStorage(Map.ofArray [|
            "a", [| for _ in 1 .. 2 do yield 1uy |]
        |])

        do! assertFileDownloaded fileCache fileStorage "a" 2UL
        assertCacheState Array.empty
    }

    [<Fact>]
    member _.``Cleanup should be triggered``(): Task = task {
        use fileCache = setUpFileCache 129UL
        use fileStorage = new WebFileStorage(Map.ofArray [|
            "a", [| for _ in 1 .. 128 do yield 1uy |]
            "b", [| for _ in 1 .. 128 do yield 2uy |]
            "c", [| 3uy |]
        |])

        do! assertFileDownloaded fileCache fileStorage "a" 128UL
        assertCacheState [| "a", fileStorage.Content("a") |]

        do! assertFileDownloaded fileCache fileStorage "b" 128UL
        assertCacheState [| "b", fileStorage.Content("b") |]

        do! assertFileDownloaded fileCache fileStorage "c" 1UL
        assertCacheState [|
            "b", fileStorage.Content("b")
            "c", fileStorage.Content("c")
        |]
    }

    [<Fact>]
    member _.``File cache cleanup works in order by file modification dates``(): Task = task {
        use fileCache = setUpFileCache 2UL
        use fileStorage = new WebFileStorage(Map.ofArray [|
            "a", [| 1uy |]
            "b", [| 2uy |]
            "c", [| 3uy |]
        |])

        do! assertFileDownloaded fileCache fileStorage "a" 1UL
        do! assertFileDownloaded fileCache fileStorage "c" 1UL
        do! assertFileDownloaded fileCache fileStorage "b" 1UL // "a" should be deleted
        assertCacheState [| "b", [| 2uy |]
                            "c", [| 3uy |] |]
        do! assertFileDownloaded fileCache fileStorage "a" 1UL // "c" should be deleted
        assertCacheState [| "a", [| 1uy |]
                            "b", [| 2uy |] |]
    }

    [<Fact>]
    member _.``File should be downloaded even if it was cleaned up during download``(): Task = task {
        use fileCache = setUpFileCache (1024UL * 1024UL)
        use fileStorage = new WebFileStorage(Map.ofArray [|
            "a", [| for _ in 1 .. 1024 * 1024 do 1uy |]
            "b", [| for _ in 1 .. 1024 * 1024 do 2uy |]
        |])

        // Start downloading the "a" item:
        let! stream = fileCache.Download(fileStorage.Link "a", "a", 1024UL * 1024UL)
        let stream = Option.get stream
        // Just keep the stream open for now and trigger the cleanup:
        do! assertFileDownloaded fileCache fileStorage "b" (1024UL * 1024UL)
        // Now there's only "b" item in the cache:
        assertCacheState [| "b", fileStorage.Content "b" |]
        // We should still be able to read "a" fully:
        let! content = readAllBytes "a (deleted from disk)" stream
        Assert.Equal<byte>(fileStorage.Content "a", content)
    }

    [<Fact>]
    member _.``File should be re-downloaded after cleanup even if there's a outdated read session in progress``(): Task = task {
        let size = 2UL * 1024UL * 1024UL
        use fileCache = setUpFileCache size
        use fileStorage = new WebFileStorage(Map.ofArray [|
            "a", [| for _ in 1UL .. size do 1uy |]
            "b", [| for _ in 1UL .. size do 2uy |]
        |])

        // Start downloading the "a" item:
        let! stream = fileCache.Download(fileStorage.Link "a", "a", size)
        let stream = Option.get stream
        // Just keep the stream open for now and trigger the cleanup:
        do! assertFileDownloaded fileCache fileStorage "b" size
        // Now there's only "b" item in the cache:
        assertCacheState [| "b", fileStorage.Content "b" |]
        // And now, while still having "a" not downloaded, let's fill the cache with it again (could be broken on
        // Windows due to peculiarity of file deletion when opened, see
        // https://boostgsoc13.github.io/boost.afio/doc/html/afio/FAQ/deleting_open_files.html):
        do! assertFileDownloaded fileCache fileStorage "a" size
        assertCacheState [| "a", fileStorage.Content "a" |]
        // We should still be able to read "a" fully:
        let! content = readAllBytes "a (deleted from disk and recreated)" stream
        Assert.Equal<byte>(fileStorage.Content "a", content)
    }
