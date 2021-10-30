module Emulsion.Tests.Telegram.LinkGeneratorTests

open System

open Emulsion.ContentProxy
open Funogram.Telegram.Types
open Xunit

open Emulsion.Database
open Emulsion.Settings
open Emulsion.Telegram
open Emulsion.Tests.TestUtils

let private hostingSettings = {
    BaseUri = Uri "https://example.com"
    HashIdSalt = "mysalt"
}
let private chatName = "test_chat"
let private fileId = "123456"

let private messageTemplate =
    { defaultMessage with
        Chat =
            { defaultChat with
                Type = SuperGroup
                Username = Some chatName
            }
    }

let private messageWithDocument =
    { messageTemplate with
        Document = Some {
            FileId = fileId
            Thumb = None
            FileName = None
            MimeType = None
            FileSize = None
        }
    }

let private messageWithAudio =
    { messageTemplate with
        Audio = Some {
            FileId = fileId
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
            FileId = fileId
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
        Photo = Some(upcast [|{
            FileId = fileId
            Width = 0
            Height = 0
            FileSize = None
        }|])
    }

let private messageWithSticker =
    { messageTemplate with
        Sticker = Some {
            FileId = fileId
            Width = 0
            Height = 0
            IsAnimated = false
            Thumb = None
            Emoji = None
            SetName = None
            MaskPosition = None
            FileSize = None
        }
    }

let private messageWithVideo =
    { messageTemplate with
        Video = Some {
            FileId = fileId
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
            FileId = fileId
            Duration = 0
            MimeType = None
            FileSize = None
        }
    }

let private messageWithVideoNote =
    { messageTemplate with
        VideoNote = Some {
            FileId = fileId
            Length = 0
            Duration = 0
            Thumb = None
            FileSize = None
        }
    }

let private doBasicLinkTest message =
    let links = Async.RunSynchronously(LinkGenerator.gatherLinks None None message)
    Assert.Equal(Some <| Uri $"https://t.me/{chatName}/{message.MessageId}", links.ContentLink)

let private doDatabaseLinkTest fileId message =
    Async.RunSynchronously <| TestDataStorage.doWithDatabase(fun databaseSettings ->
        async {
            let! links = LinkGenerator.gatherLinks (Some databaseSettings) (Some hostingSettings) message
            let link = (Option.get links.ContentLink).ToString()
            let baseUri = hostingSettings.BaseUri.ToString()
            Assert.StartsWith(baseUri, link)
            let emptyLinkLength = (Proxy.getLink hostingSettings.BaseUri "").ToString().Length
            let id = link.Substring(emptyLinkLength)
            let! content = DataStorage.transaction databaseSettings (fun context ->
                ContentStorage.getById context (Proxy.decodeHashId hostingSettings.HashIdSalt id)
            )

            Assert.Equal(message.MessageId, content.MessageId)
            Assert.Equal(message.Chat.Username, Some content.ChatUserName)
            Assert.Equal(fileId, content.FileId)
        }
    )

[<Fact>]
let documentLinkTest(): unit = doBasicLinkTest messageWithDocument

[<Fact>]
let databaseDocumentTest(): unit = doDatabaseLinkTest fileId messageWithDocument

[<Fact>]
let databaseAudioTest(): unit = doDatabaseLinkTest fileId messageWithAudio

[<Fact>]
let databaseAnimationTest(): unit = doDatabaseLinkTest fileId messageWithAnimation

[<Fact>]
let databasePhotoTest(): unit = doDatabaseLinkTest fileId messageWithPhoto

[<Fact>]
let databaseStickerTest(): unit = doDatabaseLinkTest fileId messageWithSticker

[<Fact>]
let databaseVideoTest(): unit = doDatabaseLinkTest fileId messageWithVideo

[<Fact>]
let databaseVoiceTest(): unit = doDatabaseLinkTest fileId messageWithVoice

[<Fact>]
let databaseVideoNoteTest(): unit = doDatabaseLinkTest fileId messageWithVideoNote

[<Fact>]
let multiplePhotosTest(): unit = Assert.True false
