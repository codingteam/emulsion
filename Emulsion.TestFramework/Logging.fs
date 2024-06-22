// SPDX-FileCopyrightText: 2024 Emulsion contributors <https://github.com/codingteam/emulsion>
//
// SPDX-License-Identifier: MIT

module Emulsion.TestFramework.Logging

open Serilog
open Xunit.Abstractions

let xunitLogger (output: ITestOutputHelper): ILogger =
    LoggerConfiguration().MinimumLevel.Debug().WriteTo.TestOutput(output).CreateLogger()
