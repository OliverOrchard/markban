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

    public static async Task ExecuteAsync(string rootPath, string identifier, string tag, string message, bool dryRun = false)
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

        var items = WorkItemStore.LoadAll(rootPath);
        var item = items.FirstOrDefault(i =>
            i.Id.Equals(identifier, StringComparison.OrdinalIgnoreCase) ||
            i.Slug.Equals(identifier, StringComparison.OrdinalIgnoreCase));

        if (item == null)
        {
            Console.Error.WriteLine($"Error: Work item '{identifier}' not found.");
            return;
        }

        var repoRoot = Path.GetDirectoryName(rootPath)!;
        var commitMessage = $"{tag}: {message}";

        var lanes = WorkItemStore.LoadConfig(rootPath);
        var doneLanes = lanes.Where(l => l.Type == "done").ToList();
        if (doneLanes.Count == 0)
        {
            Console.Error.WriteLine("Error: No lane with type 'done' configured. Add \"type\": \"done\" to a lane in markban.json.");
            return;
        }
        if (doneLanes.Count > 1)
        {
            Console.Error.WriteLine($"Error: Multiple lanes have type 'done' ({string.Join(", ", doneLanes.Select(l => l.Name))}). Only one is allowed.");
            return;
        }
        var doneLane = doneLanes[0];

        if (dryRun)
        {
            Console.WriteLine("=== DRY RUN — nothing will be changed ===");
            Console.WriteLine();
            Console.WriteLine($"Would move '{item.Id}' ({item.Slug}) from {item.Status} -> {doneLane.Name}");
            Console.WriteLine();
            Console.WriteLine("Current git status:");
            await RunGitAsync(repoRoot, "status", "--short");
            Console.WriteLine();
            Console.WriteLine("Would run:");
            Console.WriteLine($"  git add .");
            Console.WriteLine($"  git commit -m \"{commitMessage}\"");
            Console.WriteLine($"  git push");
            Console.WriteLine();
            Console.WriteLine("=== End dry run. No changes made. ===");
            return;
        }

        Console.WriteLine($"Moving '{item.Id}' ({item.Slug}) to {doneLane.Name}...");
        MoveCommand.Execute(rootPath, identifier, doneLane.Name);

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
