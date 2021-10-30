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
    HashIdSalt = "mySalt"
}
let private chatName = "test_chat"
let private fileId1 = "123456"
let private fileId2 = "654321"

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
            FileId = fileId1
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
            FileId = fileId1
            Width = 0
            Height = 0
            FileSize = None
        }|])
    }

let private messageWithMultiplePhotos =
    { messageWithPhoto with
        Photo = Some(Seq.append (Option.get messageWithPhoto.Photo) [|{
            FileId = fileId2
            Width = 0
            Height = 0
            FileSize = None
        }|])
    }

let private messageWithSticker =
    { messageTemplate with
        Sticker = Some {
            FileId = fileId1
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
            FileId = fileId1
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
            Duration = 0
            MimeType = None
            FileSize = None
        }
    }

let private messageWithVideoNote =
    { messageTemplate with
        VideoNote = Some {
            FileId = fileId1
            Length = 0
            Duration = 0
            Thumb = None
            FileSize = None
        }
    }

let private doBasicLinkTest message =
    let links = Async.RunSynchronously(LinkGenerator.gatherLinks None None message)
    let expectedUri = Seq.singleton <| Uri $"https://t.me/{chatName}/{message.MessageId}"
    Assert.Equal<Uri>(expectedUri, links.ContentLinks)

let private doDatabaseLinksTest (fileIds: string[]) message =
    Async.RunSynchronously <| TestDataStorage.doWithDatabase(fun databaseSettings ->
        async {
            let! links = LinkGenerator.gatherLinks (Some databaseSettings) (Some hostingSettings) message
            let contentLinks = Seq.toArray links.ContentLinks
            for fileId, link in Seq.zip fileIds contentLinks do
                let link = link.ToString()
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
