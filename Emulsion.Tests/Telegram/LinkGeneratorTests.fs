﻿module Emulsion.Tests.Telegram.LinkGeneratorTests

open System

open Emulsion.ContentProxy
open Funogram.Telegram.Types
open Serilog.Core
open Xunit

open Emulsion.Database
open Emulsion.Settings
open Emulsion.Telegram
open Emulsion.Tests.TestUtils

let private hostingSettings = {
    ExternalUriBase = Uri "https://example.com"
    BindUri = Uri "http://localhost:5556"
    HashIdSalt = "mySalt"
}
let private chatName = "test_chat"
let private fileId1 = "123456"
let private fileId2 = "654321"
let private fileId3 = "555555"

let private messageTemplate =
    Message.Create(
        messageId = 0L,
        date = DateTime.MinValue,
        chat = Chat.Create(
            id = 0L,
            ``type`` = ChatType.SuperGroup,
            username = chatName
        )
    )

let private messageWithDocument =
    { messageTemplate with
        Document = Some {
            FileId = fileId1
            FileUniqueId = fileId1
            Thumb = None
            FileName = None
            MimeType = None
            FileSize = None
        }
    }

let private messageWithAudio =
    { messageTemplate with
        Audio = Some {
            FileId = fileId1
            FileUniqueId = fileId1
            FileName = None
            Duration = 0
            Performer = None
            Title = None
            MimeType = None
            FileSize = None
            Thumb = None
        }
    }

let private messageWithAnimation =
    { messageTemplate with
        Animation = Some {
            FileId = fileId1
            FileUniqueId = fileId1
            Width = 0
            Height = 0
            Duration = 0
            Thumb = None
            FileName = None
            MimeType = None
            FileSize = None
        }
    }

let private messageWithPhoto =
    { messageTemplate with
        Photo = Some([|{
            FileId = fileId1
            FileUniqueId = fileId1
            Width = 0
            Height = 0
            FileSize = None
        }|])
    }

let private messageWithMultiplePhotos =
    { messageWithPhoto with
        Photo = Some(Array.append (Option.get messageWithPhoto.Photo) [|{
            FileId = fileId2
            FileUniqueId = fileId2
            Width = 0
            Height = 0
            FileSize = None
        }|])
    }

let private messageWithMultiplePhotoSizes =
    // Create multiple photos with the same FileUniqueId but different file ids:
    let photoSize1 = messageWithPhoto.Photo |> Option.get |> Seq.head
    let photoSize2 = { photoSize1 with FileId = fileId2; Width = photoSize1.Width + 1L; Height = photoSize1.Height + 1L }
    let photoSize3 = { photoSize1 with FileId = fileId3 }

    Assert.Equal(photoSize1.FileUniqueId, photoSize2.FileUniqueId)
    Assert.Equal(photoSize1.FileUniqueId, photoSize3.FileUniqueId)

    Assert.NotEqual<string>(photoSize1.FileId, photoSize2.FileId)
    Assert.NotEqual<string>(photoSize1.FileId, photoSize3.FileId)
    Assert.NotEqual<string>(photoSize2.FileId, photoSize3.FileId)

    { messageWithPhoto with Photo = Some [| photoSize1; photoSize2; photoSize3 |] }

let private messageWithSticker =
    { messageTemplate with
        Sticker = Some {
            FileId = fileId1
            FileUniqueId = fileId1
            Width = 0
            Height = 0
            IsAnimated = false
            Thumb = None
            Emoji = None
            SetName = None
            MaskPosition = None
            FileSize = None
            IsVideo = false
            PremiumAnimation = None
        }
    }

let private messageWithVideo =
    { messageTemplate with
        Video = Some {
            FileId = fileId1
            FileUniqueId = fileId1
            FileName = None
            Width = 0
            Height = 0
            Duration = 0
            Thumb = None
            MimeType = None
            FileSize = None
        }
    }

let private messageWithVoice =
    { messageTemplate with
        Voice = Some {
            FileId = fileId1
            FileUniqueId = fileId1
            Duration = 0
            MimeType = None
            FileSize = None
        }
    }

let private messageWithVideoNote =
    { messageTemplate with
        VideoNote = Some {
            FileId = fileId1
            FileUniqueId = fileId1
            Length = 0
            Duration = 0
            Thumb = None
            FileSize = None
        }
    }

let private doBasicLinkTest message =
    let links = Async.RunSynchronously(LinkGenerator.gatherLinks Logger.None None None message)
    let expectedUri = Seq.singleton <| Uri $"https://t.me/{chatName}/{message.MessageId}"
    Assert.Equal<Uri>(expectedUri, links.ContentLinks)

let private doDatabaseLinksTest (fileIds: string[]) message =
    Async.RunSynchronously <| TestDataStorage.doWithDatabase(fun databaseSettings ->
        async {
            let! links = LinkGenerator.gatherLinks Logger.None (Some databaseSettings) (Some hostingSettings) message
            let contentLinks = Seq.toArray links.ContentLinks
            for fileId, link in Seq.zip fileIds contentLinks do
                let link = link.ToString()
                let baseUri = hostingSettings.ExternalUriBase.ToString()
                Assert.StartsWith(baseUri, link)
                let emptyLinkLength = (Proxy.getLink hostingSettings.ExternalUriBase "").ToString().Length
                let id = link.Substring(emptyLinkLength)
                let! content = DataStorage.transaction databaseSettings (fun context ->
                    ContentStorage.getById context (Proxy.decodeHashId hostingSettings.HashIdSalt id)
                )
                let content = Option.get content

                Assert.Equal(message.MessageId, content.MessageId)
                Assert.Equal(message.Chat.Username, Some content.ChatUserName)
                Assert.Equal(fileId, content.FileId)

            Assert.Equal(fileIds.Length, contentLinks.Length)
        }
    )

let private doDatabaseLinkTest fileId message =
    doDatabaseLinksTest [|fileId|] message

[<Fact>]
let documentLinkTest(): unit = doBasicLinkTest messageWithDocument

[<Fact>]
let databaseDocumentTest(): unit = doDatabaseLinkTest fileId1 messageWithDocument

[<Fact>]
let databaseAudioTest(): unit = doDatabaseLinkTest fileId1 messageWithAudio

[<Fact>]
let databaseAnimationTest(): unit = doDatabaseLinkTest fileId1 messageWithAnimation

[<Fact>]
let databasePhotoTest(): unit = doDatabaseLinkTest fileId1 messageWithPhoto

[<Fact>]
let databaseStickerTest(): unit = doDatabaseLinkTest fileId1 messageWithSticker

[<Fact>]
let databaseVideoTest(): unit = doDatabaseLinkTest fileId1 messageWithVideo

[<Fact>]
let databaseVoiceTest(): unit = doDatabaseLinkTest fileId1 messageWithVoice

[<Fact>]
let databaseVideoNoteTest(): unit = doDatabaseLinkTest fileId1 messageWithVideoNote

[<Fact>]
let databaseMultiplePhotosTest(): unit = doDatabaseLinksTest [|fileId1; fileId2|] messageWithMultiplePhotos

[<Fact>]
let databaseMultiplePhotoSizesTest(): unit = doDatabaseLinksTest [| fileId2 |] messageWithMultiplePhotoSizes
