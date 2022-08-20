namespace Emulsion.ContentProxy

open System
open System.IO
open System.Security.Cryptography
open System.Text

open System.Threading
open Emulsion.Settings
open Serilog

type DownloadRequest = {
    Uri: Uri
    CacheKey: string
    Size: uint64
}

// TODO: Total cache limit
type FileCache(logger: ILogger,
               settings: FileCacheSettings,
               sha256: SHA256) =

    let getFilePath (cacheKey: string) =
        let hash =
            cacheKey
            |> Encoding.UTF8.GetBytes
            |> sha256.ComputeHash
            |> Convert.ToBase64String
        Path.Combine(settings.Directory, hash)

    let getFromCache(cacheKey: string) = async {
        let path = getFilePath cacheKey
        return
            if File.Exists path then
                Some(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Delete))
            else
                None
    }

    // TODO: Check total item size, too
    let ensureFreeCache size = async {
        if size > settings.FileSizeLimitBytes then
            return false
        else
            return failwith "TODO: Sanity check that cache only has files"
    }

    let download uri: Async<Stream> = async {
        return failwithf "TODO: Download the URI and return a stream"
    }

    let downloadIntoCacheAndGet uri cacheKey: Async<Stream> = async {
        let! ct = Async.CancellationToken
        let! stream = download uri
        let path = getFilePath cacheKey
        logger.Information("Saving {Uri} to path {Path}…", uri, path)

        use cachedFile = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None)
        do! Async.AwaitTask(stream.CopyToAsync(cachedFile, ct))

        let! file = getFromCache cacheKey
        return upcast Option.get file
    }

    let cancellation = new CancellationTokenSource()
    let processRequest request: Async<Stream option> = async {
        logger.Information("Cache lookup for content {Uri} (cache key {CacheKey})", request.Uri, request.CacheKey)
        match! getFromCache request.CacheKey with
        | Some content ->
            logger.Information("Cache hit for content {Uri} (cache key {CacheKey})", request.Uri, request.CacheKey)
            return Some content
        | None ->
            logger.Information("No cache hit for content {Uri} (cache key {CacheKey}), will download", request.Uri, request.CacheKey)
            let! shouldCache = ensureFreeCache request.Size
            if shouldCache then
                logger.Information("Resource {Uri} (cache key {CacheKey}, {Size} bytes) will fit into cache, caching", request.Uri, request.CacheKey, request.Size)
                let! result = downloadIntoCacheAndGet request.Uri request.CacheKey
                logger.Information("Resource {Uri} (cache key {CacheKey}, {Size} bytes) downloaded", request.Uri, request.CacheKey, request.Size)
                return Some result
            else
                logger.Information("Resource {Uri} (cache key {CacheKey}) won't fit into cache, directly downloading", request.Uri, request.CacheKey)
                let! result = download request.Uri
                return Some result
    }

    let rec processLoop(processor: MailboxProcessor<_ * AsyncReplyChannel<_>>) = async {
        while true do
            let! request, replyChannel = processor.Receive()
            try
                let! result = processRequest request
                replyChannel.Reply result
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
