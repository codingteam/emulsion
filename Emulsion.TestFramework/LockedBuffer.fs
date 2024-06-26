// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

namespace Emulsion.TestFramework

type LockedBuffer<'T>() =
    let messages = ResizeArray<'T>()
    member _.Add(m: 'T) =
        lock messages (fun () ->
            messages.Add m
        )
    member _.Count(): int =
        lock messages (fun () ->
            messages.Count
        )
    member _.All(): 'T seq =
        ResizeArray messages
