// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

module Emulsion.ExceptionUtils

open System.Runtime.ExceptionServices

let reraise (ex: exn): 'a =
    let edi = ExceptionDispatchInfo.Capture ex
    edi.Throw()
    failwith "Impossible"
