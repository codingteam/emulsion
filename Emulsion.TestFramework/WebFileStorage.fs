namespace Emulsion.TestFramework

open System

type WebFileStorage(data: Map<string, byte[]>) =
    member _.Link(entry: string): Uri =
        failwith "todo"

    member _.Content(entry: string): byte[] =
        failwith "todo"

    interface IDisposable with
        member this.Dispose(): unit = failwith "todo"

