module Emulsion.Tests.TestUtils.Logging

open Serilog
open Xunit.Abstractions

let xunitLogger (output: ITestOutputHelper): ILogger =
    LoggerConfiguration().MinimumLevel.Debug().WriteTo.TestOutput(output).CreateLogger()
