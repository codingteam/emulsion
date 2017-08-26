module Emulsion.Program

open System
open System.IO

open Microsoft.Extensions.Configuration

open Emulsion.Settings
open Emulsion.Xmpp

let private startXmpp settings = new Robot(Console.WriteLine, settings)
let private startTelegram = Telegram.run

let private getConfiguration directory fileName =
    let config =
        ConfigurationBuilder()
            .SetBasePath(directory)
            .AddJsonFile(fileName)
            .Build()
    Settings.read config

let private startApp config =
    printfn "Loaded settings: %A" config
    Console.ReadKey()
    use xmpp = startXmpp config.xmpp
    startTelegram config.telegram

    0

let private defaultConfigFileName = "emulsion.json"

[<EntryPoint>]
let main = function
    | [| |] ->
        getConfiguration (Directory.GetCurrentDirectory()) defaultConfigFileName
        |> startApp
    | [| configPath |] ->
        getConfiguration (Path.GetDirectoryName configPath) (Path.GetFileName configPath)
        |> startApp
    | _ ->
        printfn "Arguments: [config file name] (%s by default)" defaultConfigFileName
        0
