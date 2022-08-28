module Emulsion.TestFramework.StreamUtils

open System.IO
open Serilog

let readAllBytes (logger: ILogger) (id: string) (stream: Stream) = async {
    use buffer = new MemoryStream()
    let! ct = Async.CancellationToken
    logger.Information("Reading stream {Id}…", id)
    do! Async.AwaitTask(stream.CopyToAsync(buffer, ct))
    logger.Information("Successfully read stream {Id}.", id)
    return buffer.ToArray()
}
