using System.Text;
using System.Text.RegularExpressions;

public static class RenameCommand
{
    public static void Execute(string rootPath, string identifier, string newTitle, bool dryRun = false)
    {
        var items = WorkItemStore.LoadAll(rootPath);
        var item = items.FirstOrDefault(i =>
            i.Id.Equals(identifier, StringComparison.OrdinalIgnoreCase) ||
            i.Slug.Equals(identifier, StringComparison.OrdinalIgnoreCase));

        if (item == null)
        {
            Console.Error.WriteLine($"Error: Work item '{identifier}' not found.");
            return;
        }

        var settings = WorkItemStore.LoadSettings(rootPath);
        if (!SlugHelper.IsValidCasing(settings.SlugCasing))
        {
            Console.Error.WriteLine($"Error: Invalid slug casing '{settings.SlugCasing}' in markban.json. Valid values: kebab, snake, camel, pascal.");
            return;
        }

        var newSlug = SlugHelper.Generate(newTitle, settings.SlugCasing);
        if (string.IsNullOrEmpty(newSlug))
        {
            Console.Error.WriteLine("Error: New title produces an empty slug.");
            return;
        }

        var newFileName = string.IsNullOrEmpty(item.Id) ? $"{newSlug}.md" : $"{item.Id}-{newSlug}.md";
        var newPath = Path.Combine(Path.GetDirectoryName(item.FullPath)!, newFileName);

        if (dryRun)
        {
            Console.WriteLine($"Would rename: {item.FileName} -> {newFileName}");
            if (settings.HeadingEnabled)
            {
                var previewH1 = string.IsNullOrEmpty(item.Id) ? $"# {newTitle}" : $"# {item.Id} - {newTitle}";
                Console.WriteLine($"Would update H1 to: {previewH1}");
            }
            Console.WriteLine($"Would update cross-references: [{item.Slug}] -> [{newSlug}]");
            return;
        }

        var content = File.ReadAllText(item.FullPath, new UTF8Encoding(false));

        if (settings.HeadingEnabled)
        {
            var newH1 = string.IsNullOrEmpty(item.Id) ? $"# {newTitle}" : $"# {item.Id} - {newTitle}";
            var h1Pattern = new Regex(@"^#\s+.+$", RegexOptions.Multiline);
            content = h1Pattern.Replace(content, newH1, 1);
        }

        File.WriteAllText(item.FullPath, content, new UTF8Encoding(false));

        if (!item.FullPath.Equals(newPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Move(item.FullPath, newPath);
        }

        // Update cross-references across all board files
        var allItems = WorkItemStore.LoadAll(rootPath);
        foreach (var other in allItems)
        {
            if (other.FullPath.Equals(newPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var otherContent = File.ReadAllText(other.FullPath, new UTF8Encoding(false));
            var updated = otherContent.Replace($"[{item.Slug}]", $"[{newSlug}]");
            if (updated != otherContent)
            {
                File.WriteAllText(other.FullPath, updated, new UTF8Encoding(false));
            }
        }

        var displayId = string.IsNullOrEmpty(item.Id) ? item.Slug : $"{item.Id} - {item.Slug}";
        Console.WriteLine($"Renamed '{displayId}' to '{newFileName}'.");
    }
}
