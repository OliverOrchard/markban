using System.Diagnostics;
using System.Text.RegularExpressions;

public static class CommitCommand
{
    public static readonly string[] ValidTags =
    [
        "feat", "fix", "docs", "style", "refactor", "test", "build", "ci", "chore", "revert", "perf"
    ];

    public static IReadOnlyList<string> GetValidTags(string rootPath)
    {
        var configured = WorkItemStore.LoadSettings(rootPath).CommitTags;
        return configured != null && configured.Count > 0 ? configured : ValidTags;
    }

    public static Task ExecuteAsync(string rootPath, string identifier, string tag, string message, bool dryRun = false)
        => ExecuteAsync(rootPath, [identifier], tag, message, dryRun);

    public static async Task ExecuteAsync(string rootPath, IReadOnlyList<string> identifiers, string tag, string message, bool dryRun = false)
    {
        var settings = WorkItemStore.LoadSettings(rootPath);
        var validTags = GetValidTags(rootPath);

        if (!validTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"Error: Invalid tag '{tag}'. Valid tags: {string.Join(", ", validTags)}");
            return;
        }

        tag = tag.ToLowerInvariant();
        message = message.Trim();

        var msgError = ValidateMessage(message, settings.CommitMaxMessageLength);
        if (msgError != null)
        {
            Console.Error.WriteLine($"Error: {msgError}");
            return;
        }

        var lanes = WorkItemStore.LoadConfig(rootPath);
        var doneLane = ResolveDoneLane(lanes);
        if (doneLane == null)
        {
            return;
        }

        var items = WorkItemStore.LoadAll(rootPath);
        var resolved = ResolveItems(identifiers, items);
        if (resolved == null)
        {
            return;
        }

        var repoRoot = Path.GetDirectoryName(rootPath)!;
        var commitMessage = $"{tag}: {message}";

        if (dryRun)
        {
            PrintDryRun(resolved, doneLane.Name, commitMessage);
            await RunGitAsync(repoRoot, "status", "--short");
            Console.WriteLine();
            Console.WriteLine("Would run:");
            Console.WriteLine($"  git add .");
            Console.WriteLine($"  git commit -m \"{commitMessage}\"");
            Console.WriteLine($"  git push");

            var fb = settings.FeatureBranches;
            if (fb?.Enabled == true)
            {
                if (!string.IsNullOrWhiteSpace(fb.PrCommand))
                {
                    Console.WriteLine($"  {fb.PrCommand}   (prCommand)");
                }

                if (fb.CheckoutOnDone)
                {
                    Console.WriteLine($"  git checkout {fb.MainBranch} && git pull");
                }
            }

            Console.WriteLine();
            Console.WriteLine("=== End dry run. No changes made. ===");
            return;
        }

        foreach (var item in resolved)
        {
            Console.WriteLine($"Moving '{item.Id}' ({item.Slug}) to {doneLane.Name}...");
            MoveCommand.Execute(rootPath, item.Id, doneLane.Name);
        }

        Console.WriteLine();
        Console.WriteLine("git add .");
        int addExit = await RunGitAsync(repoRoot, "add", ".");
        if (addExit != 0)
        {
            Console.Error.WriteLine("Error: git add failed.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"git commit -m \"{commitMessage}\"");
        int commitExit = await RunGitAsync(repoRoot, "commit", "-m", commitMessage);
        if (commitExit != 0)
        {
            Console.Error.WriteLine("Error: git commit failed.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine("git push");
        int pushExit = await RunGitAsync(repoRoot, "push");
        if (pushExit != 0)
        {
            Console.Error.WriteLine("Error: git push failed.");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"Done. Committed and pushed: {commitMessage}");

        await RunFeatureBranchPostCommitAsync(rootPath, settings, resolved, doneLane.Name);
    }

    private static async Task RunFeatureBranchPostCommitAsync(
        string rootPath,
        BoardSettings settings,
        IReadOnlyList<WorkItem> items,
        string doneLaneName)
    {
        var fb = settings.FeatureBranches;
        if (fb?.Enabled != true)
        {
            return;
        }

        // Detect if we're on a feature branch
        var repoRoot = Path.GetDirectoryName(rootPath)!;
        var branch = await GetCurrentBranchAsync(repoRoot);
        var expectedPrefixes = items.Select(i => fb.BranchPrefix + i.Slug).ToList();
        if (!expectedPrefixes.Any(p => branch?.StartsWith(p, StringComparison.OrdinalIgnoreCase) == true))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(fb.PrCommand))
        {
            Console.WriteLine();
            Console.WriteLine($"Opening PR: {fb.PrCommand}");
            int prExit = await RunShellCommandAsync(repoRoot, fb.PrCommand);
            if (prExit != 0)
            {
                Console.Error.WriteLine("Error: PR command failed. Branch pushed; open the PR manually.");
                return;
            }
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("No prCommand configured. Open a pull request manually from your branch.");
        }

        if (fb.CheckoutOnDone)
        {
            Console.WriteLine();
            Console.WriteLine($"Returning to {fb.MainBranch}...");
            await RunGitAsync(repoRoot, "checkout", fb.MainBranch);
            await RunGitAsync(repoRoot, "pull");
        }
    }

    private static async Task<string?> GetCurrentBranchAsync(string repoRoot)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.ArgumentList.Add("branch");
        psi.ArgumentList.Add("--show-current");
        using var proc = Process.Start(psi)!;
        var output = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return output.Trim();
    }

    private static async Task<int> RunShellCommandAsync(string workingDir, string command)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "cmd",
            WorkingDirectory = workingDir,
            UseShellExecute = false
        };
        psi.ArgumentList.Add("/c");
        psi.ArgumentList.Add(command);
        using var proc = Process.Start(psi)!;
        await proc.WaitForExitAsync();
        return proc.ExitCode;
    }

    private static LaneConfig? ResolveDoneLane(IReadOnlyList<LaneConfig> lanes)
    {
        var doneLanes = lanes.Where(l => l.Type == "done").ToList();
        if (doneLanes.Count == 0)
        {
            Console.Error.WriteLine("Error: No lane with type 'done' configured. Add \"type\": \"done\" to a lane in markban.json.");
            return null;
        }

        if (doneLanes.Count > 1)
        {
            Console.Error.WriteLine($"Error: Multiple lanes have type 'done' ({string.Join(", ", doneLanes.Select(l => l.Name))}). Only one is allowed.");
            return null;
        }

        return doneLanes[0];
    }

    private static IReadOnlyList<WorkItem>? ResolveItems(IReadOnlyList<string> identifiers, IReadOnlyList<WorkItem> items)
    {
        var resolved = new List<WorkItem>();
        foreach (var id in identifiers)
        {
            var item = items.FirstOrDefault(i =>
                i.Id.Equals(id, StringComparison.OrdinalIgnoreCase) ||
                i.Slug.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (item == null)
            {
                Console.Error.WriteLine($"Error: Work item '{id}' not found.");
                return null;
            }

            resolved.Add(item);
        }

        return resolved;
    }

    private static void PrintDryRun(IReadOnlyList<WorkItem> items, string doneLane, string commitMessage)
    {
        Console.WriteLine("=== DRY RUN — nothing will be changed ===");
        Console.WriteLine();
        foreach (var item in items)
        {
            Console.WriteLine($"Would move '{item.Id}' ({item.Slug}) from {item.Status} -> {doneLane}");
        }
        Console.WriteLine();
        Console.WriteLine("Current git status:");
    }

    internal static string? ValidateMessage(string message, int maxLen = 72)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Message cannot be empty.";
        }

        if (message.Length > maxLen)
        {
            return $"Message is too long ({message.Length} chars, max {maxLen}). Keep it to one short sentence.";
        }

        // Detect multiple sentences: sentence-ending punctuation followed by more text
        if (Regex.IsMatch(message, @"[.!?]\s+\S"))
        {
            return "Message looks like more than one sentence. Keep commits to a single, concise thought.";
        }

        // Detect excessive conjunctions that suggest a run-on list
        var andCount = Regex.Matches(message, @"\band\b", RegexOptions.IgnoreCase).Count;
        if (andCount >= 3)
        {
            return "Message looks like a run-on list. Pick the one key thing this commit does.";
        }

        return null;
    }

    private static async Task<int> RunGitAsync(string workingDir, params string[] gitArgs)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDir,
            UseShellExecute = false,
        };
        foreach (var arg in gitArgs)
        {
            psi.ArgumentList.Add(arg);
        }

        using var proc = Process.Start(psi)!;
        await proc.WaitForExitAsync();
        return proc.ExitCode;
    }
}
