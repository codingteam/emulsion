module Emulsion.Settings

open Microsoft.Extensions.Configuration

type XmppSettings =
    { Login : string
      Password : string
      Room : string
      Nickname : string }

type TelegramSettings =
    { Token : string
      GroupId : string }

type LogSettings = {
    Directory: string
}

type EmulsionSettings = {
    Xmpp : XmppSettings
    Telegram : TelegramSettings
    Log: LogSettings
}

let read (config : IConfiguration) : EmulsionSettings =
    let readXmpp (section : IConfigurationSection) =
        { Login = section.["login"]
          Password = section.["password"]
          Room = section.["room"]
          Nickname = section.["nickname"] }
    let readTelegram (section : IConfigurationSection) =
        { Token = section.["token"]
          GroupId = section.["groupId"] }
    let readLog(section: IConfigurationSection) = {
        Directory = section.["directory"]
    }

    { Xmpp = readXmpp <| config.GetSection("xmpp")
      Telegram = readTelegram <| config.GetSection("telegram")
      Log = readLog <| config.GetSection "log" }
