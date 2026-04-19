using System.Diagnostics;

/// <summary>
/// Implements <c>markban start &lt;id&gt;</c>.
/// When feature branches are enabled: checks out main, pulls, creates a feature branch, and moves the item.
/// When disabled: moves the item to the next active lane.
/// </summary>
public static class StartCommand
{
    public static async Task ExecuteAsync(
        string rootPath,
        string identifier,
        bool noPull = false,
        bool dryRun = false)
    {
        var settings = WorkItemStore.LoadSettings(rootPath);
        var lanes = WorkItemStore.LoadConfig(rootPath);
        var items = WorkItemStore.LoadAll(rootPath);

        var item = items.FirstOrDefault(i =>
            i.Id.Equals(identifier, StringComparison.OrdinalIgnoreCase) ||
            i.Slug.Equals(identifier, StringComparison.OrdinalIgnoreCase));

        if (item == null)
        {
            Console.Error.WriteLine($"Error: Work item '{identifier}' not found.");
            return;
        }

        var nextLane = FindNextActiveLane(lanes, item.Status);
        if (nextLane == null)
        {
            Console.Error.WriteLine($"Error: No next active lane found after '{item.Status}'.");
            return;
        }

        var fb = settings.FeatureBranches;
        if (fb?.Enabled != true)
        {
            if (dryRun)
            {
                Console.WriteLine($"[dry-run] Would move {item.Id} from '{item.Status}' to '{nextLane}'.");
                return;
            }

            MoveCommand.Execute(rootPath, identifier, nextLane);
            return;
        }

        // Feature branch mode
        var branchName = $"{fb.BranchPrefix}{item.Slug}";

        if (dryRun)
        {
            var pullStep = (!noPull && fb.PullOnStart)
                ? $"\n  git checkout {fb.MainBranch} && git pull"
                : "";
            Console.WriteLine($"[dry-run] Would start feature branch for {item.Id}:{pullStep}");
            Console.WriteLine($"  git checkout -b {branchName}");
            Console.WriteLine($"  Move {item.Id} '{item.Status}' -> '{nextLane}'");
            return;
        }

        if (await IsWorkingTreeDirtyAsync(rootPath))
        {
            Console.Error.WriteLine("Error: Working tree has uncommitted changes. Commit or stash before starting.");
            return;
        }

        if (!noPull && fb.PullOnStart)
        {
            Console.WriteLine($"Switching to {fb.MainBranch} and pulling...");
            var (checkoutOk, checkoutErr) = await RunGitAsync(rootPath, "checkout", fb.MainBranch);
            if (!checkoutOk)
            {
                Console.Error.WriteLine($"Error: git checkout {fb.MainBranch} failed: {checkoutErr}");
                return;
            }

            var (pullOk, pullErr) = await RunGitAsync(rootPath, "pull");
            if (!pullOk)
            {
                Console.Error.WriteLine($"Error: git pull failed: {pullErr}");
                return;
            }
        }

        Console.WriteLine($"Creating branch {branchName}...");
        var (branchOk, branchErr) = await RunGitAsync(rootPath, "checkout", "-b", branchName);
        if (!branchOk)
        {
            Console.Error.WriteLine($"Error: git checkout -b {branchName} failed: {branchErr}");
            return;
        }

        MoveCommand.Execute(rootPath, identifier, nextLane);
        Console.WriteLine($"Started work on {item.Id}. Branch: {branchName}");
    }

    private static string? FindNextActiveLane(IReadOnlyList<LaneConfig> lanes, string currentLane)
    {
        var ordered = lanes.Where(l => l.Ordered).ToList();
        var idx = ordered.FindIndex(l => l.Name.Equals(currentLane, StringComparison.OrdinalIgnoreCase));
        if (idx < 0)
        {
            return ordered.FirstOrDefault(l => l.Type != "done" && l.Type != "ready" && l.Ordered)?.Name;
        }

        for (var i = idx + 1; i < ordered.Count; i++)
        {
            if (ordered[i].Type != "done")
            {
                return ordered[i].Name;
            }
        }

        return null;
    }

    private static async Task<bool> IsWorkingTreeDirtyAsync(string rootPath)
    {
        var (_, output) = await RunGitCaptureAsync(rootPath, "status", "--porcelain");
        return !string.IsNullOrWhiteSpace(output);
    }

    private static async Task<(bool Success, string Output)> RunGitAsync(string workingDir, params string[] args)
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

        using var proc = Process.Start(psi)!;
        var stderr = await proc.StandardError.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return (proc.ExitCode == 0, stderr.Trim());
    }

    private static async Task<(bool Success, string Output)> RunGitCaptureAsync(string workingDir, params string[] args)
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

        using var proc = Process.Start(psi)!;
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return (proc.ExitCode == 0, stdout.Trim());
    }
}
