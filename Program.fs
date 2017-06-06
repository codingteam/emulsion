open System

open Ctor.Xmpp

[<EntryPoint>]
let main [| login; password; room |] =
    use robot = new Robot(Console.WriteLine, login, password, room, "хортолёт")
    Console.ReadKey() |> ignore
    0
