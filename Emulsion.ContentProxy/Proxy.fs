module Emulsion.ContentProxy.Proxy

open System
open System.Collections
open HashidsNet

let encodeHashId (salt: string) (id: int64): string =
    let hashids = Hashids salt

    // Since hashids.net doesn't support negative numbers, we'll have to split our integer into three groups: 2 bits, 31
    // bits and 31 bits.
    let low = int id &&& 0x7FFFFFFF
    let middle = int(id >>> 31) &&& 0x7FFFFFFF
    let high = int(id >>> 62)

    let hashId = hashids.Encode(high, middle, low)
    if hashId = "" then failwith $"Cannot generate hashId for id {id}"
    hashId

let decodeHashId (salt: string) (hashId: string): int64 =
    let hashids = Hashids salt
    let numbers = hashids.Decode hashId |> Array.map int64
    match numbers with
    | [| high; middle; low |] -> (high <<< 62) ||| (middle <<< 31) ||| low
    | _ -> failwith($"Invalid numbers decoded from hashId {hashId}: [" + (String.concat ", " (Seq.map string numbers)) + "]")

let getLink (baseUri: Uri) (hashId: string): Uri =
    Uri(baseUri, $"content/{hashId}")
