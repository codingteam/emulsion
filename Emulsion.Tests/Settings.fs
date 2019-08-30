module Emulsion.Tests.Settings

open System
open System.IO

open FSharp.Control.Tasks
open Microsoft.Extensions.Configuration
open Xunit

open Emulsion
open Emulsion.Settings

let private testConfigText = @"{
   ""xmpp"": {
       ""login"": ""login"",
       ""password"": ""password"",
       ""room"": ""room"",
       ""nickname"": ""nickname""
   },
   ""telegram"": {
       ""token"": ""token"",
       ""groupId"": ""groupId""
   },
   ""log"": {
       ""directory"": ""/tmp/""
   }
}"

let private testConfiguration = {
    Xmpp = {
        Login = "login"
        Password = "password"
        Room = "room"
        Nickname = "nickname"
    }
    Telegram = {
        Token = "token"
        GroupId = "groupId"
    }
    Log = {
        Directory = "/tmp/"
    }
}

let private mockConfiguration() =
    let path = Path.GetTempFileName()
    task {
        do! File.WriteAllTextAsync(path, testConfigText)
        return ConfigurationBuilder().AddJsonFile(path).Build()
    }


[<Fact>]
let ``Settings read properly`` () =
    task {
        let! configuration = mockConfiguration()
        Assert.Equal(testConfiguration, Settings.read configuration)
    }
