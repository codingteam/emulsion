// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

namespace Emulsion.ContentProxy

open System.Net.Http

type SimpleHttpClientFactory() =
    interface IHttpClientFactory with
        member this.CreateClient _ = new HttpClient()
