module Emulsion.Tests.TestUtils.Exceptions

open System
open Microsoft.EntityFrameworkCore

let rec unwrap<'a when 'a :> Exception>(ex: Exception): 'a option =
    match ex with
    | :? 'a as ex -> Some ex
    | :? AggregateException as ax when ax.InnerExceptions.Count = 1 -> unwrap(Seq.exactlyOne ax.InnerExceptions)
    | :? DbUpdateException as dx when not(isNull dx.InnerException) -> unwrap dx.InnerException
    | _ -> failwithf $"Unable to unwrap the following exception into {typeof<'a>.FullName}:\n{ex}"
