module Emulsion.ExceptionUtils

open System.Runtime.ExceptionServices

let reraise (ex: exn): 'a =
    let edi = ExceptionDispatchInfo.Capture ex
    edi.Throw()
    failwith "Impossible"
