module Emulsion.Tests.Logging

open Serilog
open Xunit.Abstractions

let xunitLogger (output: ITestOutputHelper): ILogger =
    upcast LoggerConfiguration().WriteTo.TestOutput(output).CreateLogger()
