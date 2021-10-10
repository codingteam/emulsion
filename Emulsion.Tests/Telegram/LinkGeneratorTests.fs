module Emulsion.Tests.Telegram.LinkGeneratorTests

open Funogram.Telegram.Types
open Xunit

open Emulsion.Database
open Emulsion.Telegram

let private databaseSettings = ()
let private linkBase = "https://example.com"
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
    let links = Async.RunSynchronously(LinkGenerator.gatherLinks None message)
    Assert.Equal(Some $"https://t.me/{chatName}/{message.MessageId}", links.ContentLink)

let private doDatabaseLinkTest fileId message =
    Async.RunSynchronously <| async {
        let! links = LinkGenerator.gatherLinks (Some databaseSettings) message
        let link = Option.get links.ContentLink
        Assert.StartsWith(linkBase, link)
        let id = link.Substring(linkBase.Length + 1)
        let! content = DataStorage.getById databaseSettings id

        Assert.Equal(message.MessageId, content.MessageId)
        Assert.Equal(message.Chat.Username, Some content.ChatUsername)
        Assert.Equal(fileId, content.FileId)
    }

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
