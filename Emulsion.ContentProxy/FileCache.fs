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
    ReplyChannel: AsyncReplyChannel<Stream option>
}

// TODO: Total cache limit
// TODO: Threading
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
        do! Async.SwitchToThreadPool()
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
        let! stream = download uri
        let path = getFilePath cacheKey
        logger.Information("Saving {Uri} to path {Path}…", uri, path)

        do! Async.SwitchToThreadPool()
        use cachedFile = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None)
        do! Async.AwaitTask(stream.CopyToAsync cachedFile)

        match! getFromCache cacheKey with
        | Some

    }

    let cancellation = new CancellationTokenSource()
    let processRequest request = async {
        logger.Information("Cache lookup for content {Uri} (cache key {CacheKey})", uri, cacheKey)
        match! getFromCache cacheKey with
        | Some content ->
            logger.Information("Cache hit for content {Uri} (cache key {CacheKey})", uri, cacheKey)
            return Some content
        | None ->
            logger.Information("No cache hit for content {Uri} (cache key {CacheKey}), will download", uri, cacheKey)
            let! shouldCache = ensureFreeCache size
            if shouldCache then
                logger.Information("Resource {Uri} (cache key {CacheKey}, {Size} bytes) will fit into cache, caching", uri, cacheKey, size)
                let! result = downloadIntoCacheAndGet uri cacheKey
                logger.Information("Resource {Uri} (cache key {CacheKey}, {Size} bytes) downloaded", uri, cacheKey, size)
                return Some result
            else
                logger.Information("Resource {Uri} (cache key {CacheKey}) won't fit into cache, directly downloading", uri, cacheKey)
                let! result = download uri
                return Some result
    }
    let rec processLoop (processor: MailboxProcessor<_>) = async {
        while true do
            try
                let! message = processor.Re()
                do! processRequest message processor
            with
            | ex -> logger.Error(ex, "Exception while processing the file download queue")
    }
    let processor = MailboxProcessor.Start(processLoop, cancellation.Token)

    interface IDisposable with
        member _.Dispose() =
            cancellation.Dispose()
            (processor :> IDisposable).Dispose()

    member _.DownloadLink(uri: Uri, cacheKey: string, size: uint64): Async<Stream option> = processor.PostAndReply(fun chan -> {
        Uri = uri
        CacheKey = cacheKey
        Size = size
        ReplyChannel = chan
    })
