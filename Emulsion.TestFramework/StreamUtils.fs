module Emulsion.TestFramework.StreamUtils

open System.IO

let readAllBytes(stream: Stream) = async {
    use buffer = new MemoryStream()
    let! ct = Async.CancellationToken
    do! Async.AwaitTask(stream.CopyToAsync(buffer, ct))
    return buffer.ToArray()
}
