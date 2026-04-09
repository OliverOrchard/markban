using System.Text;
using System.Text.RegularExpressions;

public static class OverviewCommand
{
    private static readonly Regex TitlePattern = new(@"^#\s+(?:\d+[a-z]?\s*[-\u2013]\s*)?(.+)$", RegexOptions.Multiline);

    public static void Execute(string rootPath)
    {
        var items = WorkItemStore.LoadAll(rootPath);

        var kanban = items.Where(i => i.Status is "Todo" or "In Progress" or "Testing" or "Done").ToList();
        int total = kanban.Count;
        int done = kanban.Count(i => i.Status == "Done");
        int testing = kanban.Count(i => i.Status == "Testing");
        int inProgress = kanban.Count(i => i.Status == "In Progress");
        int todo = kanban.Count(i => i.Status == "Todo");
        int pct = total > 0 ? (int)Math.Round(100.0 * done / total) : 0;

        var sb = new StringBuilder();

        // Progress bar
        int barWidth = 30;
        int filled = total > 0 ? (int)Math.Round((double)barWidth * done / total) : 0;
        sb.Append("[");
        sb.Append(new string('#', filled));
        sb.Append(new string('.', barWidth - filled));
        sb.AppendLine($"] {pct}% -- {done} done, {testing} testing, {inProgress} active, {todo} todo ({total} total)");
        sb.AppendLine();

        // Active lanes with titles
        var activeLanes = new[] { "In Progress", "Testing", "Todo" };

        foreach (var lane in activeLanes)
        {
            var laneItems = kanban.Where(i => i.Status == lane).ToList();
            if (laneItems.Count == 0) continue;

            sb.AppendLine($"{lane} ({laneItems.Count}):");
            foreach (var item in laneItems)
            {
                var title = ExtractTitle(item);
                var prefix = string.IsNullOrEmpty(item.Id) ? "  " : $"  {item.Id}. ";
                sb.AppendLine($"{prefix}{title}");
            }
            sb.AppendLine();
        }

        // Ideas/Rejected counts only
        var ideas = items.Count(i => i.Status == "Ideas");
        var rejected = items.Count(i => i.Status == "Rejected");
        if (ideas > 0 || rejected > 0)
        {
            sb.AppendLine($"Backlog: {ideas} idea(s), {rejected} rejected");
        }

        Console.Write(sb.ToString());
    }

    private static string ExtractTitle(WorkItem item)
    {
        var match = TitlePattern.Match(item.Content);
        if (match.Success)
            return match.Groups[1].Value.Trim();

        return System.Globalization.CultureInfo.CurrentCulture.TextInfo
            .ToTitleCase(item.Slug.Replace("-", " "));
    }
}
