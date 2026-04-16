using System.Diagnostics;

namespace Markban.IntegrationTests.Infrastructure;

public static class CliRunner
{
    public static async Task<CliResult> RunAsync(string dllPath, string rootPath, params string[] args)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add(dllPath);
        psi.ArgumentList.Add("--root");
        psi.ArgumentList.Add(rootPath);
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var proc = Process.Start(psi)!;
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        return new CliResult(
            (await stdoutTask).TrimEnd(),
            (await stderrTask).TrimEnd(),
            proc.ExitCode);
    }

    /// <summary>
    /// Runs the CLI from <paramref name="workingDirectory"/> WITHOUT injecting --root, so
    /// WorkItemStore.FindRoot() is invoked and must discover the board via markban.json or
    /// the work-items walk-up heuristic.
    /// </summary>
    public static async Task<CliResult> RunInDirAsync(string dllPath, string workingDirectory, params string[] args)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory
        };
        psi.ArgumentList.Add(dllPath);
        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var proc = Process.Start(psi)!;
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        return new CliResult(
            (await stdoutTask).TrimEnd(),
            (await stderrTask).TrimEnd(),
            proc.ExitCode);
    }
}

public record CliResult(string StdOut, string StdErr, int ExitCode);
