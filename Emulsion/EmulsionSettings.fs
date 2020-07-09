module Emulsion.Settings

open System
open System.Globalization

open Microsoft.Extensions.Configuration

type XmppSettings = {
    Login: string
    Password: string
    Room: string
    RoomPassword: string option
    Nickname: string
    MessageTimeout: TimeSpan
}

type TelegramSettings = {
    Token: string
    GroupId: int64
}

type LogSettings = {
    Directory: string
}

type EmulsionSettings = {
    Xmpp : XmppSettings
    Telegram : TelegramSettings
    Log: LogSettings
}

let defaultMessageTimeout = TimeSpan.FromMinutes 5.0

let read (config : IConfiguration) : EmulsionSettings =
    let readXmpp (section : IConfigurationSection) = {
        Login = section.["login"]
        Password = section.["password"]
        Room = section.["room"]
        RoomPassword = Option.ofObj section.["roomPassword"]
        Nickname = section.["nickname"]
        MessageTimeout =
            section.["messageTimeout"]
            |> Option.ofObj
            |> Option.map (fun s -> TimeSpan.Parse(s, CultureInfo.InvariantCulture))
            |> Option.defaultValue defaultMessageTimeout
    }
    let readTelegram (section : IConfigurationSection) = {
        Token = section.["token"]
        GroupId = int64 section.["groupId"]
    }
    let readLog(section: IConfigurationSection) = {
        Directory = section.["directory"]
    }

    { Xmpp = readXmpp <| config.GetSection("xmpp")
      Telegram = readTelegram <| config.GetSection("telegram")
      Log = readLog <| config.GetSection "log" }
