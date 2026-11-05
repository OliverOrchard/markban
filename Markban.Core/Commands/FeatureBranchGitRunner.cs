using System.Diagnostics;

public static class FeatureBranchGitRunner
{
    public record ProcessOutput(int ExitCode, string StdOut, string StdErr)
    {
        public bool Success => ExitCode == 0;

        public string ErrorMessage => !string.IsNullOrWhiteSpace(StdErr)
            ? StdErr
            : StdOut;
    }

    public static string ResolveRepoRoot(string rootPath)
    {
        var dir = new DirectoryInfo(rootPath);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git"))
                || File.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Path.GetDirectoryName(rootPath) ?? rootPath;
    }

    public static async Task<ProcessOutput> RunGitAsync(string workingDir, params string[] args)
    {
        var psi = CreateGitStartInfo(workingDir, args);
        return await ExecuteProcessAsync(psi);
    }

    public static async Task<bool> IsWorkingTreeDirtyAsync(string workingDir)
    {
        var output = await RunGitAsync(workingDir, "status", "--porcelain");
        return !output.Success || !string.IsNullOrWhiteSpace(output.StdOut);
    }

    public static async Task<string?> GetCurrentBranchAsync(string workingDir)
    {
        var output = await RunGitAsync(workingDir, "branch", "--show-current");
        return output.Success && !string.IsNullOrWhiteSpace(output.StdOut)
            ? output.StdOut
            : null;
    }

    public static async Task<int> GetAheadCommitCountAsync(string workingDir, string baseBranch)
    {
        var output = await RunGitAsync(workingDir, "rev-list", "--count", $"{baseBranch}..HEAD");
        return output.Success && int.TryParse(output.StdOut, out int count)
            ? count
            : 0;
    }

    public static async Task<bool> HasUpstreamBranchAsync(string workingDir)
    {
        var output = await RunGitAsync(
            workingDir, "rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}");
        return output.Success && !string.IsNullOrWhiteSpace(output.StdOut);
    }

    public static async Task<ProcessOutput> RunShellCommandAsync(string workingDir, string command)
    {
        var psi = CreateShellStartInfo(workingDir, command);
        return await ExecuteProcessAsync(psi);
    }

    private static ProcessStartInfo CreateGitStartInfo(string workingDir, string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        return psi;
    }

    private static ProcessStartInfo CreateShellStartInfo(string workingDir, string command)
    {
        bool isWindows = OperatingSystem.IsWindows();
        var psi = new ProcessStartInfo
        {
            FileName = isWindows ? "cmd" : "/bin/sh",
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        psi.ArgumentList.Add(isWindows ? "/c" : "-c");
        psi.ArgumentList.Add(command);
        return psi;
    }

    private static async Task<ProcessOutput> ExecuteProcessAsync(ProcessStartInfo psi)
    {
        using var proc = Process.Start(psi)!;
        var stdoutTask = proc.StandardOutput.ReadToEndAsync();
        var stderrTask = proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();

        string stdout = (await stdoutTask).Trim();
        string stderr = (await stderrTask).Trim();
        return new ProcessOutput(proc.ExitCode, stdout, stderr);
    }
}
