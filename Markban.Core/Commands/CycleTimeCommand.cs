using System.Diagnostics;
using System.Text.RegularExpressions;

/// <summary>
/// Computes cycle time for work items from git history.
/// Cycle time = days from first move into an active lane to first move into a done lane.
/// </summary>
public static class CycleTimeCommand
{
    public record CycleTimeEntry(
        string Id,
        string Slug,
        string? Title,
        string? StartedDate,
        string? CompletedDate,
        double? DaysInProgress);

    public static async Task<List<CycleTimeEntry>> ExecuteAsync(string rootPath)
    {
        var lanes = WorkItemStore.LoadConfig(rootPath);
        var doneLanes = new HashSet<string>(
            lanes.Where(l => l.Type == "done").Select(l => l.Name),
            StringComparer.OrdinalIgnoreCase);
        var readyLanes = new HashSet<string>(
            lanes.Where(l => l.Type == "ready").Select(l => l.Name),
            StringComparer.OrdinalIgnoreCase);

        var events = await GetAllWorkItemEventsAsync(rootPath);
        return ComputeCycleTimes(events, doneLanes, readyLanes);
    }

    public static void PrintResults(List<CycleTimeEntry> entries)
    {
        if (entries.Count == 0)
        {
            Console.WriteLine("No completed work items found in git history.");
            return;
        }

        Console.WriteLine($"{"ID",-8} {"Title",-35} {"Started",-12} {"Completed",-12} {"Days",6}");
        Console.WriteLine(new string('-', 78));

        foreach (var entry in entries.OrderBy(e => e.CompletedDate))
        {
            var id = entry.Id.PadRight(8)[..Math.Min(8, entry.Id.Length)].PadRight(8);
            var title = (entry.Title ?? entry.Slug).PadRight(35)[..Math.Min(35, (entry.Title ?? entry.Slug).Length)].PadRight(35);
            var started = (entry.StartedDate ?? "—").PadRight(12);
            var completed = (entry.CompletedDate ?? "—").PadRight(12);
            var days = entry.DaysInProgress.HasValue
                ? entry.DaysInProgress.Value.ToString("F1").PadLeft(6)
                : "     —";
            Console.WriteLine($"{id} {title} {started} {completed} {days}");
        }

        var completedEntries = entries.Where(e => e.DaysInProgress.HasValue).ToList();
        if (completedEntries.Count > 0)
        {
            var avg = completedEntries.Average(e => e.DaysInProgress!.Value);
            Console.WriteLine(new string('-', 78));
            Console.WriteLine($"{"Average cycle time:",-58} {avg,6:F1} days  ({completedEntries.Count} item(s))");
        }
    }

    private static async Task<List<GitHistoryCommand.WorkItemEvent>> GetAllWorkItemEventsAsync(string rootPath)
    {
        var repoRoot = FindRepoRoot(rootPath);
        if (repoRoot == null)
        {
            return [];
        }

        var relPath = Path.GetRelativePath(repoRoot, rootPath).Replace('\\', '/');
        var output = await RunGitAsync(repoRoot, "log", "--format=%H|%ai|%s", "--", relPath);
        var commits = new List<GitHistoryCommand.CommitInfo>();

        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('|', 3);
            if (parts.Length == 3)
            {
                var date = parts[1].Trim();
                if (date.Length > 10)
                {
                    date = date[..10];
                }

                commits.Add(new GitHistoryCommand.CommitInfo(parts[0].Trim(), date, parts[2].Trim()));
            }
        }

        var events = new List<GitHistoryCommand.WorkItemEvent>();
        foreach (var commit in commits)
        {
            var changes = await GitHistoryCommand.GetCommitChangesAsync(repoRoot, commit.Hash);
            var wiChanges = changes
                .Where(c => IsWorkItemPath(c.Path, relPath) || IsWorkItemPath(c.OldPath, relPath))
                .ToList();

            foreach (var change in wiChanges)
            {
                var (action, fromLane, toLane) = ClassifyChangeForPath(change, relPath);
                events.Add(new GitHistoryCommand.WorkItemEvent(
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

    private static List<CycleTimeEntry> ComputeCycleTimes(
        List<GitHistoryCommand.WorkItemEvent> events,
        HashSet<string> doneLanes,
        HashSet<string> readyLanes)
    {
        // Group events by work item slug (filename without extension and id prefix)
        var byItem = events
            .GroupBy(e => NormalizeItemKey(e.WorkItemFile))
            .Where(g => !string.IsNullOrEmpty(g.Key));

        var results = new List<CycleTimeEntry>();

        foreach (var group in byItem)
        {
            var ordered = group.OrderBy(e => e.Date).ToList();
            string? startDate = null;
            string? doneDate = null;

            foreach (var ev in ordered)
            {
                var toLane = ev.ToLane;
                if (toLane == null)
                {
                    continue;
                }

                if (doneDate == null && doneLanes.Contains(toLane))
                {
                    doneDate = ev.Date;
                }
                else if (startDate == null && !readyLanes.Contains(toLane) && !doneLanes.Contains(toLane))
                {
                    startDate = ev.Date;
                }
            }

            // Only include items that have been completed
            if (doneDate == null)
            {
                continue;
            }

            double? days = null;
            if (startDate != null &&
                DateTime.TryParse(startDate, out var start) &&
                DateTime.TryParse(doneDate, out var end))
            {
                days = (end - start).TotalDays;
            }

            var fileName = Path.GetFileNameWithoutExtension(group.Key);
            var (id, slug) = ParseIdAndSlug(fileName);
            results.Add(new CycleTimeEntry(id, slug, null, startDate, doneDate, days));
        }

        return results;
    }

    private static string NormalizeItemKey(string workItemFile)
        => Path.GetFileName(workItemFile).ToLowerInvariant();

    private static (string Id, string Slug) ParseIdAndSlug(string fileName)
    {
        var m = Regex.Match(fileName, @"^(\d+[a-z]?)-(.+)$");
        return m.Success ? (m.Groups[1].Value, m.Groups[2].Value) : ("", fileName);
    }

    private static string? FindRepoRoot(string path)
    {
        var dir = new DirectoryInfo(path);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static bool IsWorkItemPath(string? path, string relBase)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        var normalized = path.Replace('\\', '/');
        return normalized.StartsWith(relBase.TrimEnd('/') + "/") && normalized.EndsWith(".md");
    }

    private static (string Action, string? FromLane, string? ToLane) ClassifyChangeForPath(
        GitHistoryCommand.FileChange change, string relBase)
    {
        var from = ExtractLaneFromPath(change.OldPath, relBase);
        var to = ExtractLaneFromPath(change.Path, relBase);

        return change.Status switch
        {
            "A" => ("Created", null, to),
            "D" => ("Deleted", from ?? to, null),
            "M" => ("Modified", null, to),
            _ when change.Status.StartsWith('R') =>
                (from != to ? "Moved" : "Renamed", from, to),
            _ => ("Unknown", null, null)
        };
    }

    private static string? ExtractLaneFromPath(string? path, string relBase)
    {
        if (string.IsNullOrEmpty(path))
        {
            return null;
        }

        var normalized = path.Replace('\\', '/');
        var prefix = relBase.TrimEnd('/') + "/";
        if (!normalized.StartsWith(prefix))
        {
            return null;
        }

        var remainder = normalized[prefix.Length..];
        var slashIdx = remainder.IndexOf('/');
        return slashIdx > 0 ? remainder[..slashIdx] : null;
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
        {
            psi.ArgumentList.Add(arg);
        }

        using var proc = Process.Start(psi)!;
        var stdout = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return stdout;
    }
}
