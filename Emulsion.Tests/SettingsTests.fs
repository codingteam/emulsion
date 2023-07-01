module Emulsion.Tests.SettingsTests

open System
open System.IO
open System.Threading.Tasks

open Microsoft.Extensions.Configuration
open Xunit

open Emulsion.Settings

let private testConfigText groupIdLiteral extendedLiteral =
    sprintf @"{
   ""xmpp"": {
       ""login"": ""login"",
       ""password"": ""password"",
       ""room"": ""room"",
       ""nickname"": ""nickname"",
       ""messageTimeout"": ""00:00:30"",
       ""pingTimeout"": ""00:00:30""
   },
   ""telegram"": {
       ""token"": ""token"",
       ""groupId"": %s
   },
   ""log"": {
       ""directory"": ""/tmp/""
   }%s
}" <| groupIdLiteral <| extendedLiteral

let private testGroupId = 200600L
let private testConfiguration = {
    Xmpp = {
        Login = "login"
        Password = "password"
        Room = "room"
        RoomPassword = None
        Nickname = "nickname"
        ConnectionTimeout = TimeSpan.FromMinutes 5.0
        MessageTimeout = TimeSpan.FromSeconds 30.0
        PingInterval = None
        PingTimeout = TimeSpan.FromSeconds 30.0
    }
    Telegram = {
        Token = "token"
        GroupId = testGroupId
        MessageThreadId = None
    }
    Log = {
        Directory = "/tmp/"
    }
    MessageArchive = {
        IsEnabled = false
    }
    Database = None
    Hosting = None
    FileCache = None
}

let private mockConfiguration groupIdLiteral extendedJson =
    let path = Path.GetTempFileName()
    task {
        do! File.WriteAllTextAsync(path, testConfigText groupIdLiteral extendedJson)
        return ConfigurationBuilder().AddJsonFile(path).Build()
    }


[<Fact>]
let ``Settings read properly`` () =
    task {
        let! configuration = mockConfiguration (string testGroupId) ""
        Assert.Equal(testConfiguration, read configuration)
    }

[<Fact>]
let ``Settings read the group id as string``(): Task<unit> =
    task {
        let! configuration = mockConfiguration "\"200600\"" ""
        Assert.Equal(testConfiguration, read configuration)
    }

[<Fact>]
let ``Extended settings read properly``(): Task = task {
    let! configuration = mockConfiguration (string testGroupId) @",
   ""database"": {
       ""dataSource"": "":memory:""
   },
   ""hosting"": {
       ""externalUriBase"": ""https://example.com"",
       ""bindUri"": ""http://localhost:5555"",
       ""hashIdSalt"": ""123123123""
   }"
    let expectedConfiguration =
        { testConfiguration with
            Database = Some {
                DataSource = ":memory:"
            }
            Hosting = Some {
                ExternalUriBase = Uri "https://example.com"
                BindUri = "http://localhost:5555"
                HashIdSalt = "123123123"
            }
        }
    Assert.Equal(expectedConfiguration, read configuration)
}
