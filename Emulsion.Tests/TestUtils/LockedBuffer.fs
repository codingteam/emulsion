namespace Emulsion.Tests.TestUtils

type LockedBuffer<'T>() =
    let messages = ResizeArray<'T>()
    member __.Add(m: 'T) =
        lock messages (fun () ->
            messages.Add m
        )
    member __.Count(): int =
        lock messages (fun () ->
            messages.Count
        )
    member __.All(): 'T seq =
        upcast ResizeArray messages
