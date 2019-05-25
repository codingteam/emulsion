module Emulsion.Telegram.Html

/// HTML escaping for Telegram only. According to the docs: https://core.telegram.org/bots/api#html-style
let escape : string -> string =
    String.collect(function
                   | '<' -> "&lt;"
                   | '>' -> "&gt;"
                   | '&' -> "&amp;"
                   | other -> other.ToString())
