module Emulsion.Tests.SettingsTests

open System.IO
open System.Threading.Tasks

open FSharp.Control.Tasks
open Microsoft.Extensions.Configuration
open Xunit

open Emulsion
open Emulsion.Settings

let private testConfigText groupIdLiteral =
    sprintf @"{
   ""xmpp"": {
       ""login"": ""login"",
       ""password"": ""password"",
       ""room"": ""room"",
       ""nickname"": ""nickname""
   },
   ""telegram"": {
       ""token"": ""token"",
       ""groupId"": %s
   },
   ""log"": {
       ""directory"": ""/tmp/""
   }
}" <| groupIdLiteral

let private testConfiguration = {
    Xmpp = {
        Login = "login"
        Password = "password"
        Room = "room"
        Nickname = "nickname"
    }
    Telegram = {
        Token = "token"
        GroupId = 200600L
    }
    Log = {
        Directory = "/tmp/"
    }
}

let private mockConfiguration groupIdLiteral =
    let path = Path.GetTempFileName()
    task {
        do! File.WriteAllTextAsync(path, testConfigText groupIdLiteral)
        return ConfigurationBuilder().AddJsonFile(path).Build()
    }


[<Fact>]
let ``Settings read properly`` () =
    task {
        let! configuration = mockConfiguration "200600"
        Assert.Equal(testConfiguration, read configuration)
    }

[<Fact>]
let ``Settings read the group id as string``(): Task<unit> =
    task {
        let! configuration = mockConfiguration "\"200600\""
        Assert.Equal(testConfiguration, read configuration)
    }
