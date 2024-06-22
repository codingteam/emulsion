// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

module Emulsion.TestFramework.FileCacheUtil

open System.IO

open Emulsion.ContentProxy
open Emulsion.Settings
open Emulsion.TestFramework.Logging

let newCacheDirectory() =
    let path = Path.GetTempFileName()
    File.Delete path
    Directory.CreateDirectory path |> ignore
    path

let setUpFileCache outputHelper sha256 cacheDirectory (totalLimitBytes: uint64) =
    let settings = {
        Directory = cacheDirectory
        FileSizeLimitBytes = 10UL * 1024UL * 1024UL
        TotalCacheSizeLimitBytes = totalLimitBytes
    }

    new FileCache(xunitLogger outputHelper, settings, SimpleHttpClientFactory(), sha256)
