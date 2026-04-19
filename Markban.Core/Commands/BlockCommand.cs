using System.Text;

/// <summary>Commands for marking work items as blocked/unblocked via frontmatter.</summary>
public static class BlockCommand
{
    private const string BlockedField = "blocked";

    public static void Block(string rootPath, string identifier, string reason)
    {
        var settings = WorkItemStore.LoadSettings(rootPath);
        if (!settings.BlockedEnabled)
        {
            Console.Error.WriteLine("Error: The 'blocked' feature is disabled in your config.");
            return;
        }

        var item = FindItem(rootPath, identifier);
        if (item == null)
        {
            return;
        }

        var content = File.ReadAllText(item.FullPath, new UTF8Encoding(false));
        var updated = FrontmatterParser.SetField(content, BlockedField, reason);
        File.WriteAllText(item.FullPath, updated, new UTF8Encoding(false));

        Console.WriteLine($"Marked '{item.Id}' ({item.Slug}) as blocked: {reason}");
    }

    public static void Unblock(string rootPath, string identifier)
    {
        var settings = WorkItemStore.LoadSettings(rootPath);
        if (!settings.BlockedEnabled)
        {
            Console.Error.WriteLine("Error: The 'blocked' feature is disabled in your config.");
            return;
        }

        var item = FindItem(rootPath, identifier);
        if (item == null)
        {
            return;
        }

        var content = File.ReadAllText(item.FullPath, new UTF8Encoding(false));
        var updated = FrontmatterParser.RemoveField(content, BlockedField);
        File.WriteAllText(item.FullPath, updated, new UTF8Encoding(false));

        Console.WriteLine($"Unblocked '{item.Id}' ({item.Slug})'.");
    }

    public static void ListBlocked(string rootPath)
    {
        var settings = WorkItemStore.LoadSettings(rootPath);
        if (!settings.BlockedEnabled)
        {
            Console.Error.WriteLine("Error: The 'blocked' feature is disabled in your config.");
            return;
        }

        var items = WorkItemStore.LoadAll(rootPath);
        var blocked = items
            .Select(i => (Item: i, Reason: FrontmatterParser.GetField(i.Content, BlockedField)))
            .Where(x => !string.IsNullOrEmpty(x.Reason))
            .ToList();

        if (blocked.Count == 0)
        {
            Console.WriteLine("No blocked items.");
            return;
        }

        Console.WriteLine($"{blocked.Count} blocked item(s):");
        foreach (var (item, reason) in blocked)
        {
            var id = string.IsNullOrEmpty(item.Id) ? item.Slug : item.Id;
            Console.WriteLine($"  [{id}] {item.Slug} ({item.Status}): {reason}");
        }
    }

    /// <summary>
    /// Returns true if the item's content has a non-empty 'blocked' frontmatter field.
    /// </summary>
    public static bool IsBlocked(string content)
        => !string.IsNullOrEmpty(FrontmatterParser.GetField(content, BlockedField));

    private static WorkItem? FindItem(string rootPath, string identifier)
    {
        var items = WorkItemStore.LoadAll(rootPath);
        var item = items.FirstOrDefault(i =>
            i.Id.Equals(identifier, StringComparison.OrdinalIgnoreCase) ||
            i.Slug.Equals(identifier, StringComparison.OrdinalIgnoreCase));

        if (item == null)
        {
            Console.Error.WriteLine($"Error: Work item '{identifier}' not found.");
        }

        return item;
    }
}
