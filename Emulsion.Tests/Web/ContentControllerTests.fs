namespace Emulsion.Tests.Web

open System
open System.Security.Cryptography
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
open Emulsion.TestFramework
open Emulsion.TestFramework.Logging
open Emulsion.Web

type ContentControllerTests(output: ITestOutputHelper) =

    let hostingSettings = {
        ExternalUriBase = Uri "https://example.com/emulsion"
        BindUri = "http://localhost:5557"
        HashIdSalt = "test_salt"
    }

    let logger = xunitLogger output
    let telegramClient = TelegramClientMock()
    let sha256 = SHA256.Create()

    let cacheDirectory = lazy FileCacheUtil.newCacheDirectory()

    let setUpFileCache() =
        FileCacheUtil.setUpFileCache output sha256 cacheDirectory.Value 0UL

    let performTestWithPreparation fileCache prepareAction testAction = Async.StartAsTask(async {
        return! TestDataStorage.doWithDatabase(fun databaseSettings -> async {
            do! prepareAction databaseSettings

            use loggerFactory = new SerilogLoggerFactory(logger)
            let logger = loggerFactory.CreateLogger<ContentController>()
            use context = new EmulsionDbContext(databaseSettings.ContextOptions)
            let controller = ContentController(logger, hostingSettings, telegramClient, fileCache, context)
            return! testAction controller
        })
    })

    let performTest = performTestWithPreparation None (fun _ -> async.Return())
    let performTestWithContent fileCache content = performTestWithPreparation fileCache (fun databaseOptions -> async {
        use context = new EmulsionDbContext(databaseOptions.ContextOptions)
        do! DataStorage.addAsync context.TelegramContents content
        return! Async.Ignore <| Async.AwaitTask(context.SaveChangesAsync())
    })

    interface IDisposable with
        member _.Dispose() = sha256.Dispose()

    [<Fact>]
    member _.``ContentController returns BadRequest on hashId deserialization error``(): Task =
        performTest (fun controller -> async {
            let hashId = "z-z-z-z-z"
            let! result = Async.AwaitTask <| controller.Get hashId
            Assert.IsType<BadRequestResult> result |> ignore
        })

    [<Fact>]
    member _.``ContentController returns NotFound if the content doesn't exist in the database``(): Task =
        performTest (fun controller -> async {
            let hashId = Proxy.encodeHashId hostingSettings.HashIdSalt 667L
            let! result = Async.AwaitTask <| controller.Get hashId
            Assert.IsType<NotFoundResult> result |> ignore
        })

    [<Fact>]
    member _.``ContentController returns a normal redirect if there's no file cache``(): Task =
        let contentId = 343L
        let chatUserName = "MySuperExampleChat"
        let messageId = 777L
        let content = {
            Id = contentId
            ChatUserName = chatUserName
            MessageId = messageId
            FileId = "foobar"
        }

        performTestWithContent None content (fun controller -> async {
            let hashId = Proxy.encodeHashId hostingSettings.HashIdSalt contentId
            let! result = Async.AwaitTask <| controller.Get hashId
            let redirect = Assert.IsType<RedirectResult> result
            Assert.Equal(Uri $"https://t.me/{chatUserName}/{string messageId}", Uri redirect.Url)
        })

    [<Fact>]
    member _.``ContentController returns NotFound if the content doesn't exist on the Telegram server``(): Task = task {
        let contentId = 344L
        let chatUserName = "MySuperExampleChat"
        let messageId = 777L
        let fileId = "foobar1"
        let content = {
            Id = contentId
            ChatUserName = chatUserName
            MessageId = messageId
            FileId = fileId
        }

        telegramClient.SetResponse(fileId, None)

        use fileCache = setUpFileCache()
        do! performTestWithContent (Some fileCache) content (fun controller -> async {
            let hashId = Proxy.encodeHashId hostingSettings.HashIdSalt contentId
            let! result = Async.AwaitTask <| controller.Get hashId
            Assert.IsType<NotFoundResult> result |> ignore
        })
    }

    [<Fact>]
    member _.``ContentController returns 404 if the cache reports that a file was not found``(): Task = task {
        let contentId = 344L
        let chatUserName = "MySuperExampleChat"
        let messageId = 777L
        let fileId = "foobar1"
        let content = {
            Id = contentId
            ChatUserName = chatUserName
            MessageId = messageId
            FileId = fileId
        }


        use fileCache = setUpFileCache()
        use fileStorage = new WebFileStorage(Map.empty)
        telegramClient.SetResponse(fileId, Some {
            TemporaryLink = fileStorage.Link fileId
            Size = 1UL
        })

        do! performTestWithContent (Some fileCache) content (fun controller -> async {
            let hashId = Proxy.encodeHashId hostingSettings.HashIdSalt contentId
            let! result = Async.AwaitTask <| controller.Get hashId
            Assert.IsType<NotFoundResult> result |> ignore
        })
    }

    [<Fact>]
    member _.``ContentController returns a downloaded file from cache``(): Task = task {
        let contentId = 343L
        let chatUserName = "MySuperExampleChat"
        let messageId = 777L
        let fileId = "foobar"
        let content = {
            Id = contentId
            ChatUserName = chatUserName
            MessageId = messageId
            FileId = fileId
        }

        let onServerFileId = "fileIdOnServer"
        use fileCache = setUpFileCache()
        use fileStorage = new WebFileStorage(Map.ofArray [| onServerFileId, [| 1uy; 2uy; 3uy |] |])
        let testFileInfo = {
            TemporaryLink = fileStorage.Link onServerFileId
            Size = 1UL
        }
        telegramClient.SetResponse(fileId, Some testFileInfo)

        do! performTestWithContent (Some fileCache) content (fun controller -> async {
            let hashId = Proxy.encodeHashId hostingSettings.HashIdSalt contentId
            let! result = Async.AwaitTask <| controller.Get hashId
            let streamResult = Assert.IsType<FileStreamResult> result
            let! content = StreamUtils.readAllBytes streamResult.FileStream
            Assert.Equal<byte>(fileStorage.Content onServerFileId, content)
        })
    }
