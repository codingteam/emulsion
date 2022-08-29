module Emulsion.TestFramework.Dumps

open System
open System.Diagnostics
open System.IO
open System.Reflection
open System.Threading
open System.Threading.Tasks

let private findSolutionDirectory startDir =
    let mutable currentDir = startDir
    while Seq.isEmpty(Directory.EnumerateFiles(currentDir, "*.sln")) && currentDir <> null do
        currentDir <- Path.GetDirectoryName currentDir

    if currentDir <> null then currentDir
    else failwith $"Cannot find solution in any of parent directories of path \"{startDir}\"."

let private dumpThreads pid =
    let outputDir = findSolutionDirectory(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))
    let datetime = DateTime.UtcNow.ToString("yyyyMMdd_HHmmSS")
    let outputPath = Path.Combine(outputDir, $"{pid}_{datetime}.dmp")
    let dotnet = if OperatingSystem.IsWindows() then "dotnet.exe" else "dotnet"
    let psi = ProcessStartInfo(dotnet, RedirectStandardOutput = true, RedirectStandardError = true)
    psi.ArgumentList.Add "dump"
    psi.ArgumentList.Add "collect"
    psi.ArgumentList.Add "--process-id"
    psi.ArgumentList.Add <| string pid
    psi.ArgumentList.Add "--output"
    psi.ArgumentList.Add outputPath
    let dumpProcess = Process.Start psi
    Async.RunSynchronously(async {
        let stdOut = dumpProcess.StandardOutput.ReadToEndAsync()
        let stdErr = dumpProcess.StandardError.ReadToEndAsync()
        let exit = Async.AwaitTask(dumpProcess.WaitForExitAsync())

        do! exit

        if dumpProcess.ExitCode <> 0 then failwith $"dotnet dump exit code {dumpProcess.ExitCode}. StdOut:\n{stdOut.Result}\n\nStdErr: {stdErr.Result}"
    })

    outputPath

let doWithTimeoutAndDump<'a> (timeout: TimeSpan) (action: unit -> 'a): 'a =
    let task = Task.Run(fun() -> action())
    let success = SpinWait.SpinUntil((fun() -> task.IsCompleted), timeout)

    if success then task.Result
    else
        let outputPath = dumpThreads(Process.GetCurrentProcess().Id)
        raise(TimeoutException($"Timeout waiting for {timeout}; dump saved to \"{outputPath}\"."))
