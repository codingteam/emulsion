module Emulsion.Tests.Telegram.LinkGeneratorTests

open Emulsion.Telegram
open Funogram.Telegram.Types
open Xunit

let private chatName = "test_chat"
let private messageTemplate =
    { defaultMessage with
        Chat =
            { defaultChat with
                Type = SuperGroup
                Username = Some chatName
            }
    }

let private doBasicLinkTest message =
    let links = Async.RunSynchronously(LinkGenerator.gatherLinks message)
    Assert.Equal(Some $"https://t.me/{chatName}/{message.MessageId}", links.ContentLink)

[<Fact>]
let documentLinkTest(): unit =
    doBasicLinkTest
        { messageTemplate with
            Document = Some {
                FileId = ""
                Thumb = None
                FileName = None
                MimeType = None
                FileSize = None
            }
        }
