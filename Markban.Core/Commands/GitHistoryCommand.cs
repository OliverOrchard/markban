using System.Diagnostics;
using System.Text.RegularExpressions;

public static class GitHistoryCommand
{
    public record CommitInfo(string Hash, string Date, string Message);
    public record WorkItemEvent(string Hash, string Date, string Action, string WorkItemFile, string? FromLane, string? ToLane);

    public static async Task<List<WorkItemEvent>> ExecuteAsync(string repoRoot, string filePath)
    {
        var commits = await GetCommitsForFileAsync(repoRoot, filePath);
        var events = new List<WorkItemEvent>();

        foreach (var commit in commits)
        {
            var changes = await GetCommitChangesAsync(repoRoot, commit.Hash);
            var wiChanges = changes
                .Where(c => IsWorkItemPath(c.Path) || IsWorkItemPath(c.OldPath))
                .ToList();

            foreach (var change in wiChanges)
            {
                var (action, fromLane, toLane) = ClassifyChange(change);
                events.Add(new WorkItemEvent(
                    commit.Hash,
                    commit.Date,
                    action,
                    change.Path ?? change.OldPath!,
                    fromLane,
                    toLane));
            }
        }

        return events;
    }

    public static void PrintResults(List<WorkItemEvent> events, string filePath)
    {
        if (events.Count == 0)
        {
            Console.WriteLine($"No work item activity found in the git history of '{filePath}'.");
            return;
        }

        Console.WriteLine($"Work item activity in commits touching '{filePath}':\n");

        var byCommit = events.GroupBy(e => new { e.Hash, e.Date });
        foreach (var group in byCommit)
        {
            Console.WriteLine($"  {group.Key.Hash[..7]} ({group.Key.Date}):");
            foreach (var ev in group)
            {
                var detail = ev.Action switch
                {
                    "Moved" => $"    {ev.Action}: {Path.GetFileName(ev.WorkItemFile)}  [{ev.FromLane} -> {ev.ToLane}]",
                    "Created" => $"    {ev.Action}: {Path.GetFileName(ev.WorkItemFile)}  [in {ev.ToLane}]",
                    "Deleted" => $"    {ev.Action}: {Path.GetFileName(ev.WorkItemFile)}  [from {ev.FromLane}]",
                    "Modified" => $"    {ev.Action}: {Path.GetFileName(ev.WorkItemFile)}  [in {ev.ToLane ?? ev.FromLane}]",
                    _ => $"    {ev.Action}: {Path.GetFileName(ev.WorkItemFile)}"
                };
                Console.WriteLine(detail);
            }
        }

        // Summary: unique work items involved
        var uniqueItems = events
            .Select(e => Path.GetFileName(e.WorkItemFile))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order()
            .ToList();

        Console.WriteLine($"\n  Total: {events.Count} event(s) across {byCommit.Count()} commit(s), {uniqueItems.Count} unique work item(s)");
    }

    internal static async Task<List<CommitInfo>> GetCommitsForFileAsync(string repoRoot, string filePath)
    {
        // git log with follow to track renames
        var output = await RunGitAsync(repoRoot, "log", "--follow", "--format=%H|%ai|%s", "--", filePath);
        var commits = new List<CommitInfo>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('|', 3);
            if (parts.Length == 3)
            {
                // Trim date to just YYYY-MM-DD
                var date = parts[1].Trim();
                if (date.Length > 10) date = date[..10];
                commits.Add(new CommitInfo(parts[0].Trim(), date, parts[2].Trim()));
            }
        }

        return commits;
    }

    internal record FileChange(string Status, string? OldPath, string Path);

    internal static async Task<List<FileChange>> GetCommitChangesAsync(string repoRoot, string hash)
    {
        // --diff-filter=ACDMR covers Add, Copy, Delete, Modify, Rename
        var output = await RunGitAsync(repoRoot, "diff-tree", "--no-commit-id", "-r", "--name-status", "-M", hash);
        var changes = new List<FileChange>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t');
            if (parts.Length < 2) continue;

            var status = parts[0].Trim();
            if (status.StartsWith('R') && parts.Length >= 3)
            {
                // Rename: R{score}\toldPath\tnewPath
                changes.Add(new FileChange("R", parts[1], parts[2]));
            }
            else if (status.StartsWith('C') && parts.Length >= 3)
            {
                changes.Add(new FileChange("C", parts[1], parts[2]));
            }
            else
            {
                changes.Add(new FileChange(status, null, parts[1]));
            }
        }

        return changes;
    }

    internal static bool IsWorkItemPath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        var normalized = path.Replace('\\', '/');
        return normalized.StartsWith("work-items/") && normalized.EndsWith(".md");
    }

    internal static string? ExtractLane(string? path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        var normalized = path.Replace('\\', '/');
        if (!normalized.StartsWith("work-items/")) return null;

        var remainder = normalized["work-items/".Length..];
        var slashIdx = remainder.IndexOf('/');
        return slashIdx > 0 ? remainder[..slashIdx] : null;
    }

    internal static (string Action, string? FromLane, string? ToLane) ClassifyChange(FileChange change)
    {
        return change.Status switch
        {
            "A" => ("Created", null, ExtractLane(change.Path)),
            "D" => ("Deleted", ExtractLane(change.Path ?? change.OldPath), null),
            "M" => ("Modified", null, ExtractLane(change.Path)),
            _ when change.Status.StartsWith('R') =>
                (ExtractLane(change.OldPath) != ExtractLane(change.Path) ? "Moved" : "Renamed",
                 ExtractLane(change.OldPath),
                 ExtractLane(change.Path)),
            _ when change.Status.StartsWith('C') =>
                ("Copied", ExtractLane(change.OldPath), ExtractLane(change.Path)),
            _ => ("Unknown", null, null)
        };
    }

    private static async Task<string> RunGitAsync(string workingDir, params string[] args)
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
            psi.ArgumentList.Add(arg);

        using var proc = Process.Start(psi)!;
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return stdout;
    }
}
