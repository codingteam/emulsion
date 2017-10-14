module Emulsion.Settings

open Microsoft.Extensions.Configuration
open System.Security

type XmppSettings =
    { login : string
      password : string
      room : string
      nickname : string }

type TelegramSettings =
    { token : string
      groupId : string }

type EmulsionSettings =
    { xmpp : XmppSettings
      telegram : TelegramSettings }

let read (config : IConfiguration) : EmulsionSettings =
    let readXmpp (section : IConfigurationSection) =
        { login = section.["login"]
          password = section.["password"]
          room = section.["room"]
          nickname = section.["nickname"] }
    let readTelegram (section : IConfigurationSection) =
        { token = section.["token"]
          groupId = section.["groupId"] }

    { xmpp = readXmpp <| config.GetSection("xmpp")
      telegram = readTelegram <| config.GetSection("telegram") }
