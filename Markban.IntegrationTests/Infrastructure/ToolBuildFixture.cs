using System.Diagnostics;
using Xunit;

namespace Markban.IntegrationTests.Infrastructure;

public class ToolBuildFixture : IAsyncLifetime
{
    public string DllPath { get; private set; } = "";

    public async Task InitializeAsync()
    {
        var projectDir = FindProjectDirectory();

        var psi = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("build");
        psi.ArgumentList.Add(projectDir);
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("Debug");
        psi.ArgumentList.Add("--nologo");
        psi.ArgumentList.Add("-v");
        psi.ArgumentList.Add("q");

        using var proc = Process.Start(psi)!;
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"Failed to build WorkItemViewer:\n{stderr}");

        DllPath = Path.Combine(projectDir, "bin", "Debug", "net10.0", "Markban.Cli.dll");
        if (!File.Exists(DllPath))
            throw new FileNotFoundException($"Built DLL not found at: {DllPath}");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static string FindProjectDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Markban.Cli");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not find Markban.Cli project directory.");
    }
}

[CollectionDefinition("CLI")]
public class CliCollection : ICollectionFixture<ToolBuildFixture> { }
