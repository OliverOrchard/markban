using System.Diagnostics;
using System.Text;

namespace Markban.IntegrationTests.Infrastructure;

/// <summary>
/// A throwaway git repository with a bare "origin" remote and a markban board in <c>work-items</c>.
/// </summary>
public class GitTestRepo : IDisposable
{
    private readonly string _baseDir;

    public string RepoDir { get; }

    public string RemoteDir { get; }

    public string BoardRoot => Path.Combine(RepoDir, "work-items");

    public GitTestRepo(string featureBranchesJson, string mainBranch = "main")
    {
        _baseDir = Path.Combine(Path.GetTempPath(), "mb-git-" + Guid.NewGuid().ToString("N")[..8]);
        RepoDir = Path.Combine(_baseDir, "repo");
        RemoteDir = Path.Combine(_baseDir, "remote.git");
        Directory.CreateDirectory(RepoDir);
        Directory.CreateDirectory(RemoteDir);

        Git(RemoteDir, "init", "--bare", "-b", mainBranch);
        Git(RepoDir, "init", "-b", mainBranch);
        Git(RepoDir, "config", "user.name", "Test");
        Git(RepoDir, "config", "user.email", "test@example.com");
        Git(RepoDir, "config", "commit.gpgsign", "false");
        Git(RepoDir, "remote", "add", "origin", RemoteDir);
        WriteBoard(featureBranchesJson);
        Git(RepoDir, "add", ".");
        Git(RepoDir, "commit", "-m", "chore: initial");
        Git(RepoDir, "push", "-u", "origin", mainBranch);
    }

    public bool BoardFileExists(string lane, string fileName)
        => File.Exists(Path.Combine(BoardRoot, lane, fileName));

    public string CurrentBranch() => Git(RepoDir, "branch", "--show-current");

    public string CommitsAhead(string branch, string baseBranch)
        => Git(RepoDir, "rev-list", "--count", $"{baseBranch}..{branch}");

    public string RemoteBranches() => Git(RepoDir, "ls-remote", "--heads", "origin");

    public string Status() => Git(RepoDir, "status", "--porcelain");

    public void CommitFile(string name, string content, string message)
    {
        File.WriteAllText(Path.Combine(RepoDir, name), content);
        Git(RepoDir, "add", ".");
        Git(RepoDir, "commit", "-m", message);
    }

    public void PushUpstreamCommit(string branch)
    {
        string clone = Path.Combine(_baseDir, "other-clone");
        Git(_baseDir, "clone", RemoteDir, clone);
        Git(clone, "config", "user.name", "Other");
        Git(clone, "config", "user.email", "other@example.com");
        File.WriteAllText(Path.Combine(clone, "upstream.txt"), "upstream");
        Git(clone, "add", ".");
        Git(clone, "commit", "-m", "chore: upstream change");
        Git(clone, "push", "origin", branch);
    }

    public void BreakRemote() => Git(RepoDir, "remote", "set-url", "origin", Path.Combine(_baseDir, "missing.git"));

    public string Git(string workingDir, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (string arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var proc = Process.Start(psi)!;
        string stdout = proc.StandardOutput.ReadToEnd();
        string stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit();
        if (proc.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {stderr}");
        }

        return stdout.Trim();
    }

    public void Dispose()
    {
        if (!Directory.Exists(_baseDir))
        {
            return;
        }

        foreach (string file in Directory.EnumerateFiles(_baseDir, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_baseDir, true);
    }

    private void WriteBoard(string featureBranchesJson)
    {
        File.WriteAllText(
            Path.Combine(RepoDir, "markban.json"),
            "{\"git\":{\"featureBranches\":" + featureBranchesJson + "}}",
            new UTF8Encoding(false));
        foreach (string lane in new[] { "In Progress", "Testing", "Done" })
        {
            Directory.CreateDirectory(Path.Combine(BoardRoot, lane));
            File.WriteAllText(Path.Combine(BoardRoot, lane, ".gitkeep"), "");
        }

        Directory.CreateDirectory(Path.Combine(BoardRoot, "Todo"));
        File.WriteAllText(
            Path.Combine(BoardRoot, "Todo", "1-my-task.md"),
            "# 1 - My Task\n\n## Description\n\nA task\n");
    }
}
