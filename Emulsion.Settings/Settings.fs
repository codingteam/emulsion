module Emulsion.Settings

open System
open System.Globalization

open Microsoft.Extensions.Configuration

open Emulsion.Database

type XmppSettings = {
    Login: string
    Password: string
    Room: string
    RoomPassword: string option
    Nickname: string
    ConnectionTimeout: TimeSpan
    MessageTimeout: TimeSpan
    PingInterval: TimeSpan option
    PingTimeout: TimeSpan
}

type TelegramSettings = {
    Token: string
    GroupId: int64
    MessageThreadId: int64 option
}

type LogSettings = {
    Directory: string
}

type HostingSettings = {
    ExternalUriBase: Uri
    BindUri: string
    HashIdSalt: string
}

type FileCacheSettings = {
    Directory: string
    FileSizeLimitBytes: uint64
    TotalCacheSizeLimitBytes: uint64
}

type EmulsionSettings = {
    Xmpp: XmppSettings
    Telegram: TelegramSettings
    Log: LogSettings
    Database: DatabaseSettings option
    Hosting: HostingSettings option
    FileCache: FileCacheSettings option
}

let defaultConnectionTimeout = TimeSpan.FromMinutes 5.0
let defaultMessageTimeout = TimeSpan.FromMinutes 5.0
let defaultPingTimeout = TimeSpan.FromSeconds 30.0

let private readTimeSpanOpt key (section: IConfigurationSection) =
    section[key]
    |> Option.ofObj
    |> Option.map (fun s -> TimeSpan.Parse(s, CultureInfo.InvariantCulture))

let private readTimeSpan defaultVal key section =
    readTimeSpanOpt key section
    |> Option.defaultValue defaultVal

let read (config : IConfiguration) : EmulsionSettings =
    let int64Opt: string -> int64 option = Option.ofObj >> Option.map int64
    let uint64OrDefault value ``default`` =
        value
        |> Option.ofObj
        |> Option.map uint64
        |> Option.defaultValue ``default``

    let readXmpp (section : IConfigurationSection) = {
        Login = section["login"]
        Password = section["password"]
        Room = section["room"]
        RoomPassword = Option.ofObj section["roomPassword"]
        Nickname = section["nickname"]
        ConnectionTimeout = readTimeSpan defaultConnectionTimeout "connectionTimeout" section
        MessageTimeout = readTimeSpan defaultMessageTimeout  "messageTimeout" section
        PingInterval = readTimeSpanOpt "pingInterval" section
        PingTimeout = readTimeSpan defaultPingTimeout "pingTimeout" section
    }
    let readTelegram (section : IConfigurationSection) = {
        Token = section["token"]
        GroupId = int64 section["groupId"]
        MessageThreadId = int64Opt section["messageThreadId"]
    }
    let readLog(section: IConfigurationSection) = {
        Directory = section["directory"]
    }
    let readDatabase(section: IConfigurationSection) =
        section["dataSource"]
        |> Option.ofObj
        |> Option.map(fun dataSource -> { DataSource = dataSource })
    let readHosting(section: IConfigurationSection) =
        let externalUriBase = Option.ofObj section["externalUriBase"]
        let bindUri = Option.ofObj section["bindUri"]
        let hashIdSalt = Option.ofObj section["hashIdSalt"]
        match externalUriBase, bindUri, hashIdSalt with
        | Some externalUriBase, Some bindUri, Some hashIdSalt ->
            Some {
                ExternalUriBase = Uri externalUriBase
                BindUri = bindUri
                HashIdSalt = hashIdSalt
            }
        | None, None, None -> None
        | other -> failwith $"Parameter pack {other} represents invalid hosting settings."
    let readFileCache(section: IConfigurationSection) =
        Option.ofObj section["directory"]
        |> Option.map(fun directory -> {
            Directory = directory
            FileSizeLimitBytes = uint64OrDefault section["fileSizeLimitBytes"] (1024UL * 1024UL)
            TotalCacheSizeLimitBytes = uint64OrDefault section["totalCacheSizeLimitBytes"] (20UL * 1024UL * 1024UL)
        })

    { Xmpp = readXmpp <| config.GetSection("xmpp")
      Telegram = readTelegram <| config.GetSection("telegram")
      Log = readLog <| config.GetSection "log"
      Database = readDatabase <| config.GetSection "database"
      Hosting = readHosting <| config.GetSection "hosting"
      FileCache = readFileCache <| config.GetSection "fileCache" }
