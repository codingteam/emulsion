module Emulsion.Tests.LoggingTests

open Emulsion
open JetBrains.Diagnostics
open Serilog
open Serilog.Core
open Serilog.Events
open Xunit

[<Fact>]
let ``attachToRdLogSystem should proxy the logged messages``(): unit =
    let events = ResizeArray()
    let serilogLogger = {
        new ILogger with
            override this.Write(logEvent) =
                if logEvent.Properties[Constants.SourceContextPropertyName].ToString() = "\"LoggingTests\"" then
                    lock events (fun() -> events.Add logEvent)
    }
    let rdLogger = Log.GetLog "LoggingTests"
    use _ = Logging.attachToRdLogSystem serilogLogger
    rdLogger.Info "foo"
    let event = lock events (fun() -> Assert.Single events)
    Assert.Equal(LogEventLevel.Information, event.Level)
    Assert.Equal("foo", event.MessageTemplate.Text)
    Assert.Equal("\"LoggingTests\"", event.Properties[Constants.SourceContextPropertyName].ToString())
    lock events (fun() ->
        events.Clear()

        rdLogger.Error("Test {0}", 1)
        let event = Assert.Single events
        Assert.Equal(LogEventLevel.Error, event.Level)
        Assert.Equal("Test 1", event.MessageTemplate.Text)
    )
