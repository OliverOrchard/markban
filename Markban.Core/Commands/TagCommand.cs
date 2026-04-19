using System.Text;

/// <summary>Commands for managing tags on work items via frontmatter.</summary>
public static class TagCommand
{
    private const string TagsField = "tags";

    public static void AddTags(string rootPath, string identifier, IReadOnlyList<string> tagsToAdd)
    {
        var settings = WorkItemStore.LoadSettings(rootPath);
        if (!settings.TagsEnabled)
        {
            Console.Error.WriteLine("Error: The 'tags' feature is disabled in your config.");
            return;
        }

        var item = FindItem(rootPath, identifier);
        if (item == null)
        {
            return;
        }

        var content = File.ReadAllText(item.FullPath, new UTF8Encoding(false));
        var existing = FrontmatterParser.GetListField(content, TagsField) ?? [];
        var merged = existing
            .Concat(tagsToAdd)
            .Select(t => t.ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var updated = FrontmatterParser.SetField(content, TagsField, merged);
        File.WriteAllText(item.FullPath, updated, new UTF8Encoding(false));

        Console.WriteLine($"Tags for '{item.Id}' ({item.Slug}): {string.Join(", ", merged)}");
    }

    public static void RemoveTag(string rootPath, string identifier, string tagToRemove)
    {
        var settings = WorkItemStore.LoadSettings(rootPath);
        if (!settings.TagsEnabled)
        {
            Console.Error.WriteLine("Error: The 'tags' feature is disabled in your config.");
            return;
        }

        var item = FindItem(rootPath, identifier);
        if (item == null)
        {
            return;
        }

        var content = File.ReadAllText(item.FullPath, new UTF8Encoding(false));
        var existing = FrontmatterParser.GetListField(content, TagsField) ?? [];
        var updated = existing
            .Where(t => !t.Equals(tagToRemove, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var newContent = updated.Count > 0
            ? FrontmatterParser.SetField(content, TagsField, updated)
            : FrontmatterParser.RemoveField(content, TagsField);

        File.WriteAllText(item.FullPath, newContent, new UTF8Encoding(false));
        Console.WriteLine($"Tags for '{item.Id}' ({item.Slug}): {(updated.Count > 0 ? string.Join(", ", updated) : "(none)")}");
    }

    public static void ListTags(string rootPath, string identifier)
    {
        var settings = WorkItemStore.LoadSettings(rootPath);
        if (!settings.TagsEnabled)
        {
            Console.Error.WriteLine("Error: The 'tags' feature is disabled in your config.");
            return;
        }

        var item = FindItem(rootPath, identifier);
        if (item == null)
        {
            return;
        }

        var tags = FrontmatterParser.GetListField(item.Content, TagsField);
        if (tags == null || tags.Count == 0)
        {
            Console.WriteLine($"'{item.Id}' ({item.Slug}) has no tags.");
        }
        else
        {
            Console.WriteLine($"Tags: {string.Join(", ", tags)}");
        }
    }

    /// <summary>Returns the tags for an item's content, or an empty list.</summary>
    public static List<string> GetTags(string content)
        => FrontmatterParser.GetListField(content, TagsField) ?? [];

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
