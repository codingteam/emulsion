namespace Emulsion.ContentProxy

open System
open System.IO
open System.Net.Http
open System.Security.Cryptography
open System.Text
open System.Threading

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

    let DecodeFileNameToSha256Hash(fileName: string): byte[] =
        (Base58.M4N71KR.Decode fileName).ToArray()

type FileCache(logger: ILogger,
               settings: FileCacheSettings,
               httpClientFactory: IHttpClientFactory,
               sha256: SHA256) =

    let getFilePath(cacheKey: string) =
        Path.Combine(settings.Directory, FileCache.EncodeFileName(sha256, cacheKey))

    let getFromCache(cacheKey: string) = async {
        let path = getFilePath cacheKey
        return
            if File.Exists path then
                Some(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read|||FileShare.Delete))
            else
                None
    }

    let assertCacheValid() = async {
        Directory.EnumerateFileSystemEntries settings.Directory
        |> Seq.iter(fun entry ->
            let entryName = Path.GetFileName entry

            if not <| File.Exists entry
            then failwith $"Cache directory invalid: contains a subdirectory: \"{entryName}\"."

            let hash = FileCache.DecodeFileNameToSha256Hash entryName
            if hash.Length <> sha256.HashSize / 8
            then failwith (
                $"Cache directory invalid: contains entry \"{entryName}\" which doesn't correspond to a " +
                "base58-encoded SHA-256 hash."
            )
        )
    }

    let ensureFreeCache size = async {
        if size > settings.FileSizeLimitBytes || size > settings.TotalCacheSizeLimitBytes then
            return false
        else
            do! assertCacheValid()

            let allEntries =
                Directory.EnumerateFileSystemEntries settings.Directory
                |> Seq.map FileInfo

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
                    info.Delete()

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
            use cachedFile = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)
            do! Async.AwaitTask(stream.CopyToAsync(cachedFile, ct))
            logger.Information("Download successful: \"{Uri}\" to \"{Path}\".")
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

    let rec processLoop(processor: MailboxProcessor<_ * AsyncReplyChannel<_>>) = async {
        while true do
            let! request, replyChannel = processor.Receive()
            try
                let! result = processRequest request
                replyChannel.Reply(Some result)
            with
            | ex ->
                logger.Error(ex, "Exception while processing the file download queue")
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
