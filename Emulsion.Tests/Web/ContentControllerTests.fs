namespace Emulsion.Tests.Web

open System
open System.Threading.Tasks

open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.Logging
open Serilog.Extensions.Logging
open Xunit
open Xunit.Abstractions

open Emulsion.ContentProxy
open Emulsion.Database
open Emulsion.Database.Entities
open Emulsion.Settings
open Emulsion.Telegram
open Emulsion.Tests.TestUtils
open Emulsion.Tests.TestUtils.Logging
open Emulsion.Web

type ContentControllerTests(output: ITestOutputHelper) =

    let hostingSettings = {
        ExternalUriBase = Uri "https://example.com/emulsion"
        BindUri = "http://localhost:5557"
        HashIdSalt = "test_salt"
    }

    let logger = xunitLogger output
    let telegramClient = TelegramClientMock()

    let performTestWithPreparation prepareAction testAction = Async.StartAsTask(async {
        return! TestDataStorage.doWithDatabase(fun databaseSettings -> async {
            do! prepareAction databaseSettings

            use loggerFactory = new SerilogLoggerFactory(logger)
            let logger = loggerFactory.CreateLogger<ContentController>()
            use context = new EmulsionDbContext(databaseSettings.ContextOptions)
            let controller = ContentController(logger, hostingSettings, telegramClient, None, context)
            return! testAction controller
        })
    })

    let performTest = performTestWithPreparation(fun _ -> async.Return())

    [<Fact>]
    member _.``ContentController returns BadRequest on hashId deserialization error``(): Task =
        performTest (fun controller -> async {
            let hashId = "z-z-z-z-z"
            let! result = Async.AwaitTask <| controller.Get hashId
            Assert.IsType<BadRequestResult> result |> ignore
        })

    [<Fact>]
    member _.``ContentController returns NotFound if the content doesn't exist``(): Task =
        performTest (fun controller -> async {
            let hashId = Proxy.encodeHashId hostingSettings.HashIdSalt 667L
            let! result = Async.AwaitTask <| controller.Get hashId
            Assert.IsType<NotFoundResult> result |> ignore
        })

    [<Fact>]
    member _.``ContentController returns a correct result``(): Task =
        let contentId = 343L
        let chatUserName = "MySuperExampleChat"
        let messageId = 777L
        let fileId = "foobar"

        let testLink = Uri "https://example.com/myFile"
        let testFileInfo = {
            TemporaryLink = testLink
            Size = 1UL
        }
        telegramClient.SetResponse(fileId, Some testFileInfo)

        performTestWithPreparation (fun databaseOptions -> async {
            use context = new EmulsionDbContext(databaseOptions.ContextOptions)
            let content = {
                Id = contentId
                ChatUserName = chatUserName
                MessageId = messageId
                FileId = "foobar"
            }
            do! DataStorage.addAsync context.TelegramContents content
            return! Async.Ignore <| Async.AwaitTask(context.SaveChangesAsync())
        }) (fun controller -> async {
            let hashId = Proxy.encodeHashId hostingSettings.HashIdSalt contentId
            let! result = Async.AwaitTask <| controller.Get hashId
            let redirect = Assert.IsType<RedirectResult> result
            Assert.Equal(testLink, Uri redirect.Url)
        })
