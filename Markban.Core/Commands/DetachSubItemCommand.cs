using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Promotes a lettered sub-item (e.g. <c>32a</c>) to a standalone primary item in the same lane.
/// The parent and its remaining sub-items are left untouched. Pure logic: no console output.
/// </summary>
public static class DetachSubItemCommand
{
    private static readonly Regex SubItemIdPattern = new(@"^(\d+)([a-z])$");
    private static readonly Regex HeadingPattern = new(@"^# \d+[a-z]? - (.+)$");

    public static DetachSubItemResult Execute(string rootPath, string lane, string subItemId)
    {
        if (!SubItemIdPattern.IsMatch(subItemId))
        {
            return new DetachSubItemResult(false, $"'{subItemId}' is not a sub-item id.");
        }

        var all = WorkItemStore.LoadAll(rootPath);
        var item = all.FirstOrDefault(i => i.Id == subItemId && i.Status.Equals(lane, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return new DetachSubItemResult(false, $"Sub-item '{subItemId}' not found in {lane}.");
        }

        var newId = (MaxPrimaryNumber(all) + 1).ToString();
        Rename(item, newId);
        return new DetachSubItemResult(true, $"Detached {subItemId} as {newId}.", newId);
    }

    private static int MaxPrimaryNumber(IEnumerable<WorkItem> items)
    {
        return items
            .Select(i => Regex.Match(i.Id, @"^\d+"))
            .Where(m => m.Success)
            .Select(m => int.Parse(m.Value))
            .DefaultIfEmpty(0)
            .Max();
    }

    private static void Rename(WorkItem item, string newId)
    {
        var folder = Path.GetDirectoryName(item.FullPath)!;
        var newPath = Path.Combine(folder, $"{newId}-{item.Slug}.md");
        HistoryRecorder.CaptureBefore(item.FullPath);
        File.Move(item.FullPath, newPath);
        HistoryRecorder.TrackMove(item.FullPath, newPath);
        UpdateHeading(newPath, newId);
    }

    private static void UpdateHeading(string path, string newId)
    {
        var lines = File.ReadAllLines(path, Encoding.UTF8);
        var index = ReorderCommand.FindHeadingLine(lines);
        if (index < 0 || !HeadingPattern.IsMatch(lines[index]))
        {
            return;
        }

        lines[index] = $"# {newId} - {HeadingPattern.Match(lines[index]).Groups[1].Value}";
        File.WriteAllLines(path, lines, new UTF8Encoding(false));
    }
}
