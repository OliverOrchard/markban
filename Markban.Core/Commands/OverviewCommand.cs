using System.Text;
using System.Text.RegularExpressions;

public static class OverviewCommand
{
    private static readonly Regex TitlePattern = new(@"^#\s+(?:\d+[a-z]?\s*[-\u2013]\s*)?(.+)$", RegexOptions.Multiline);

    public static void Execute(string rootPath)
    {
        var configDir = Path.GetDirectoryName(rootPath) ?? rootPath;
        IReadOnlyList<BoardEntry> boards;

        try
        {
            boards = WorkItemStore.LoadBoards(configDir);
        }
        catch (InvalidDataException ex)
        {
            Console.WriteLine($"Warning: {ex.Message}");
            Console.WriteLine();
            PrintBoardProgress(rootPath);
            return;
        }

        if (boards.Count > 0)
        {
            PrintMultiBoardOverview(boards);
            return;
        }

        PrintBoardProgress(rootPath);
    }

    private static void PrintMultiBoardOverview(IReadOnlyList<BoardEntry> boards)
    {
        foreach (var board in boards)
        {
            Console.WriteLine($"=== {board.Name} ===");
            try
            {
                var boardRoot = WorkItemStore.ResolveConfiguredBoardRoot(board.ResolvedPath);
                PrintBoardProgress(boardRoot);
            }
            catch (DirectoryNotFoundException ex)
            {
                Console.WriteLine($"Warning: {ex.Message}");
            }
            catch (InvalidDataException ex)
            {
                Console.WriteLine($"Warning: {ex.Message}");
            }

            Console.WriteLine();
        }
    }

    private static void PrintBoardProgress(string rootPath)
    {
        var lanes = WorkItemStore.LoadConfig(rootPath);
        var items = WorkItemStore.LoadAll(rootPath);

        var orderedPickable = lanes.Where(l => l.Ordered && l.Pickable).ToList();
        var doneLaneName = lanes.FirstOrDefault(l => l.Type == "done")?.Name;
        var tracked = items.Where(i => orderedPickable.Any(l => l.Name == i.Status)).ToList();

        int total = tracked.Count;
        int done = doneLaneName != null ? tracked.Count(i => i.Status == doneLaneName) : 0;
        int pct = total > 0 ? (int)Math.Round(100.0 * done / total) : 0;

        var sb = new StringBuilder();
        int barWidth = 30;
        int filled = total > 0 ? (int)Math.Round((double)barWidth * done / total) : 0;
        sb.Append("[");
        sb.Append(new string('#', filled));
        sb.Append(new string('.', barWidth - filled));

        var summaryParts = new List<string> { $"{done} done" };
        foreach (var lane in orderedPickable.Where(l => l.Name != doneLaneName).Reverse())
        {
            summaryParts.Add($"{tracked.Count(i => i.Status == lane.Name)} {lane.Name.ToLower()}");
        }

        summaryParts.Add($"{total} total");
        sb.AppendLine($"] {pct}% -- {string.Join(", ", summaryParts)}");
        sb.AppendLine();

        foreach (var lane in orderedPickable.Where(l => l.Name != doneLaneName).Reverse())
        {
            var laneItems = tracked.Where(i => i.Status == lane.Name).ToList();
            if (laneItems.Count == 0)
            {
                continue;
            }

            sb.AppendLine($"{lane.Name} ({laneItems.Count}):");
            foreach (var item in laneItems)
            {
                var title = ExtractTitle(item);
                var prefix = string.IsNullOrEmpty(item.Id) ? "  " : $"  {item.Id}. ";
                sb.AppendLine($"{prefix}{title}");
            }

            sb.AppendLine();
        }

        var backlogParts = lanes
            .Where(l => !l.Pickable)
            .Select(l => (l.Name, Count: items.Count(i => i.Status == l.Name)))
            .Where(x => x.Count > 0)
            .Select(x => $"{x.Count} {x.Name.ToLower()}")
            .ToList();
        if (backlogParts.Count > 0)
        {
            sb.AppendLine($"Backlog: {string.Join(", ", backlogParts)}");
        }

        Console.Write(sb.ToString());
    }

    private static string ExtractTitle(WorkItem item)
    {
        var match = TitlePattern.Match(item.Content);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }

        return System.Globalization.CultureInfo.CurrentCulture.TextInfo
            .ToTitleCase(item.Slug.Replace("-", " "));
    }
}
