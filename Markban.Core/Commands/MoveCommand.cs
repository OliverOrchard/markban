using System.Text;
using System.Text.RegularExpressions;

public static class MoveCommand
{
    public static void Execute(string rootPath, string identifier, string targetFolder)
    {
        var items = WorkItemStore.LoadAll(rootPath);
        var item = items.FirstOrDefault(i => i.Id == identifier || i.Slug == identifier);

        if (item == null)
        {
            Console.WriteLine($"Error: Work item '{identifier}' not found.");
            return;
        }

        var validFolders = new[] { "Todo", "In Progress", "Testing", "Done", "Ideas", "Rejected" };
        var normalizedTarget = validFolders.FirstOrDefault(f =>
            f.Equals(targetFolder, StringComparison.OrdinalIgnoreCase) ||
            f.Replace(" ", "").Equals(targetFolder.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));

        if (normalizedTarget == null)
        {
            Console.WriteLine($"Error: Target folder '{targetFolder}' is invalid. Valid: Todo, In Progress, Testing, Done, Ideas, Rejected.");
            return;
        }

        if (item.Status == normalizedTarget)
        {
            Console.WriteLine($"Item is already in '{normalizedTarget}'.");
            return;
        }

        // Determine destination filename
        string newFileName;
        if (normalizedTarget == "Ideas" || normalizedTarget == "Rejected")
        {
            // Strip number prefix when archiving: "42-slug.md" / "42a-slug.md" -> "slug.md"
            newFileName = Regex.Replace(item.FileName, @"^\d+[a-z]?-", "");
        }
        else if (item.Status == "Ideas" || item.Status == "Rejected")
        {
            // Assign next safe number when promoting an idea into the kanban
            var maxId = items
                .Where(i => i.Status != "Ideas" && i.Status != "Rejected" && !string.IsNullOrEmpty(i.Id))
                .Select(i => { var m = Regex.Match(i.Id, @"^(\d+)"); return m.Success ? int.Parse(m.Groups[1].Value) : 0; })
                .DefaultIfEmpty(0)
                .Max();
            newFileName = $"{maxId + 1}-{item.FileName}";
        }
        else
        {
            newFileName = item.FileName;
        }

        var newPath = Path.Combine(rootPath, normalizedTarget, newFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(newPath)!);

        // Sanitize and save to new location, then delete old
        var content = File.ReadAllText(item.FullPath);
        var sanitized = SanitizeCommand.SanitizeText(content);
        sanitized = SanitizeCommand.FixReferences(sanitized, items).Text;

        File.WriteAllText(newPath, sanitized, new UTF8Encoding(false));
        File.Delete(item.FullPath);

        var displayId = string.IsNullOrEmpty(item.Id) ? item.Slug : $"{item.Id} - {item.Slug}";
        Console.WriteLine($"Successfully moved '{displayId}' to {normalizedTarget} as '{newFileName}'.");

        // Auto-compact source folder if we moved from a numbered lane to Ideas/Rejected
        if ((normalizedTarget == "Ideas" || normalizedTarget == "Rejected")
            && item.Status != "Ideas" && item.Status != "Rejected")
        {
            CompactFolder(rootPath, item.Status);
        }
    }

    internal static void CompactFolder(string rootPath, string folder)
    {
        var allItems = WorkItemStore.LoadAll(rootPath);
        var folderItems = allItems
            .Where(i => i.Status == folder)
            .Select(i =>
            {
                var m = Regex.Match(i.Id, @"^(\d+)([a-z]?)$");
                return m.Success ? (Item: i, Number: int.Parse(m.Groups[1].Value), Letter: m.Groups[2].Value) : default;
            })
            .Where(x => x.Item != null)
            .OrderBy(x => x.Number).ThenBy(x => x.Letter)
            .ToList();

        var primaryNumbers = folderItems.Where(x => x.Letter == "").Select(x => x.Number).ToList();
        if (primaryNumbers.Count < 2) return;

        bool hasGaps = false;
        for (int i = 1; i < primaryNumbers.Count; i++)
        {
            if (primaryNumbers[i] != primaryNumbers[i - 1] + 1) { hasGaps = true; break; }
        }
        if (!hasGaps) return;

        Console.WriteLine($"Compacting {folder} to close gaps...");
        var orderStr = string.Join(",", primaryNumbers);
        var startNumber = primaryNumbers.Min();
        ReorderCommand.Execute(rootPath, folder, orderStr, false, false, startNumber);
    }
}
