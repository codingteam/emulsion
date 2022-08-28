namespace Emulsion.ContentProxy

open System
open System.IO
open System.Net.Http
open System.Security.Cryptography
open System.Text
open System.Threading

open JetBrains.Collections.Viewable
open Serilog
open SimpleBase

open Emulsion.Settings

type DownloadRequest = {
    Uri: Uri
    CacheKey: string
    Size: uint64
}

module Base58 =
    /// Suggested by @ttldtor.
    let M4N71KR = Base58(Base58Alphabet "123456789qwertyuiopasdfghjkzxcvbnmQWERTYUPASDFGHJKLZXCVBNM")

module FileCache =
    let EncodeFileName(sha256: SHA256, cacheKey: string): string =
        cacheKey
        |> Encoding.UTF8.GetBytes
        |> sha256.ComputeHash
        |> Base58.M4N71KR.Encode

    let TryDecodeFileNameToSha256Hash(fileName: string): byte[] option =
        try
            Some <| (Base58.M4N71KR.Decode fileName).ToArray()
        with
        | :? ArgumentException -> None

    let IsMoveAndDeleteModeEnabled =
        // NOTE: On older versions of Windows (known to reproduce on windows-2019 GitHub Actions image), the following
        // scenario may be defunct:
        //
        // - open a file with FileShare.Delete (i.e. for download)
        // - delete a file (i.e. during the cache cleanup)
        // - try to create a file with the same name again
        //
        // According to this article
        // (https://boostgsoc13.github.io/boost.afio/doc/html/afio/FAQ/deleting_open_files.html), it is impossible to do
        // since file will occupy its disk name until the last handle is closed.
        //
        // In practice, this is allowed (checked at least on Windows 10 20H2 and windows-2022 GitHub Actions image), but
        // some tests are known to be broken on older versions of Windows (windows-2019).
        //
        // As a workaround, let's rename the file to a random name before deleting it.
        //
        // This workaround may be removed after these older versions of Windows goes out of support.
        OperatingSystem.IsWindows()

type FileCache(logger: ILogger,
               settings: FileCacheSettings,
               httpClientFactory: IHttpClientFactory,
               sha256: SHA256) =

    let error = Signal<Exception>()

    let getFilePath(cacheKey: string) =
        Path.Combine(settings.Directory, FileCache.EncodeFileName(sha256, cacheKey))

    let readFileOptions =
        FileStreamOptions(Mode = FileMode.Open, Access = FileAccess.Read, Options = FileOptions.Asynchronous, Share = (FileShare.Read ||| FileShare.Delete))

    let writeFileOptions =
        FileStreamOptions(Mode = FileMode.CreateNew, Access = FileAccess.Write, Options = FileOptions.Asynchronous, Share = FileShare.None)

    let getFromCache(cacheKey: string) = async {
        let path = getFilePath cacheKey
        return
            if File.Exists path then
                Some(new FileStream(path, readFileOptions))
            else
                None
    }

    let enumerateCacheFiles() =
        let entries = Directory.EnumerateFileSystemEntries settings.Directory
        if FileCache.IsMoveAndDeleteModeEnabled then
            entries |> Seq.filter(fun p -> not(p.EndsWith ".deleted"))
        else
            entries

    let deleteFileSafe (fileInfo: FileInfo) = async {
        if FileCache.IsMoveAndDeleteModeEnabled then
            fileInfo.MoveTo(Path.Combine(fileInfo.DirectoryName, $"{Guid.NewGuid().ToString()}.deleted"))
            fileInfo.Delete()
        else
            fileInfo.Delete()
    }

    let assertCacheDirectoryExists() = async {
        Directory.CreateDirectory settings.Directory |> ignore
    }

    let assertCacheValid() = async {
        enumerateCacheFiles()
        |> Seq.iter(fun entry ->
            let entryName = Path.GetFileName entry

            if not <| File.Exists entry
            then failwith $"Cache directory invalid: contains a subdirectory \"{entryName}\"."

            match FileCache.TryDecodeFileNameToSha256Hash entryName with
            | Some hash when hash.Length = sha256.HashSize / 8 -> ()
            | _ ->
                failwith (
                    $"Cache directory invalid: contains an entry \"{entryName}\" which doesn't correspond to a " +
                    "base58-encoded SHA-256 hash."
                )
        )
    }

    let ensureFreeCache size = async {
        if size > settings.FileSizeLimitBytes || size > settings.TotalCacheSizeLimitBytes then
            return false
        else
            do! assertCacheDirectoryExists()
            do! assertCacheValid()

            let allEntries = enumerateCacheFiles() |> Seq.map FileInfo

            // Now, sort the entries from newest to oldest, and start deleting if required at a point when we understand
            // that there are too much files:
            let entriesByPriority =
                allEntries
                |> Seq.sortByDescending(fun info -> info.LastWriteTimeUtc)
                |> Seq.toArray

            let mutable currentSize = 0UL
            for info in entriesByPriority do
                currentSize <- currentSize + Checked.uint64 info.Length
                if currentSize + size > settings.TotalCacheSizeLimitBytes then
                    logger.Information("Deleting a cache item \"{FileName}\" ({Size} bytes)", info.Name, info.Length)
                    do! deleteFileSafe info

            return true
    }

    let download(uri: Uri): Async<Stream> = async {
        let! ct = Async.CancellationToken

        use client = httpClientFactory.CreateClient()
        let! response = Async.AwaitTask <| client.GetAsync(uri, ct)
        return! Async.AwaitTask <| response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync()
    }

    let downloadIntoCacheAndGet uri cacheKey: Async<Stream> = async {
        let! ct = Async.CancellationToken
        let! stream = download uri
        let path = getFilePath cacheKey
        logger.Information("Saving {Uri} to path {Path}…", uri, path)

        do! async { // to limit the cachedFile scope
            use cachedFile = new FileStream(path, writeFileOptions)
            do! Async.AwaitTask(stream.CopyToAsync(cachedFile, ct))
            logger.Information("Download successful: \"{Uri}\" to \"{Path}\".", uri, path)
        }

        let! file = getFromCache cacheKey
        return upcast Option.get file
    }

    let cancellation = new CancellationTokenSource()
    let processRequest request: Async<Stream> = async {
        logger.Information("Cache lookup for content {Uri} (cache key {CacheKey})", request.Uri, request.CacheKey)
        match! getFromCache request.CacheKey with
        | Some content ->
            logger.Information("Cache hit for content {Uri} (cache key {CacheKey})", request.Uri, request.CacheKey)
            return content
        | None ->
            logger.Information("No cache hit for content {Uri} (cache key {CacheKey}), will download", request.Uri, request.CacheKey)
            let! shouldCache = ensureFreeCache request.Size
            if shouldCache then
                logger.Information("Resource {Uri} (cache key {CacheKey}, {Size} bytes) will fit into cache, caching", request.Uri, request.CacheKey, request.Size)
                let! result = downloadIntoCacheAndGet request.Uri request.CacheKey
                logger.Information("Resource {Uri} (cache key {CacheKey}, {Size} bytes) downloaded", request.Uri, request.CacheKey, request.Size)
                return result
            else
                logger.Information("Resource {Uri} (cache key {CacheKey}) won't fit into cache, directly downloading", request.Uri, request.CacheKey)
                let! result = download request.Uri
                return result
    }

    let processLoop(processor: MailboxProcessor<_ * AsyncReplyChannel<_>>) = async {
        while true do
            let! request, replyChannel = processor.Receive()
            try
                let! result = processRequest request
                replyChannel.Reply(Some result)
            with
            | ex ->
                logger.Error(ex, "Exception while processing the file download queue")
                error.Fire ex
                replyChannel.Reply None
    }
    let processor = MailboxProcessor.Start(processLoop, cancellation.Token)

    interface IDisposable with
        member _.Dispose() =
            cancellation.Dispose()
            (processor :> IDisposable).Dispose()

    member _.Download(uri: Uri, cacheKey: string, size: uint64): Async<Stream option> =
        processor.PostAndAsyncReply(fun chan -> ({
            Uri = uri
            CacheKey = cacheKey
            Size = size
        }, chan))

    member _.Error: ISource<Exception> = error
