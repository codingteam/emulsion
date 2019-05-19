module Emulsion.Telegram.Html
open System.Text

/// HTML escaping for Telegram only. According to the docs: https://core.telegram.org/bots/api#html-style
let escape (text : string) : string =
    let result = StringBuilder text.Length
    for char in text do
        ignore <|
            match char with
            | '<' -> result.Append "&lt;"
            | '>' -> result.Append "&gt;"
            | '&' -> result.Append "&amp;"
            | other -> result.Append other
    result.ToString()
