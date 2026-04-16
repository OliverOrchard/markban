using System.Text;
using System.Text.RegularExpressions;

public static class MoveCommand
{
    public static void Execute(string rootPath, string identifier, string targetFolder, bool overrideWip = false)
    {
        WorkItemStore.EnsureLaneDirectories(rootPath);
        var items = WorkItemStore.LoadAll(rootPath);
        var item = items.FirstOrDefault(i => i.Id == identifier || i.Slug == identifier);

        if (item == null)
        {
            Console.WriteLine($"Error: Work item '{identifier}' not found.");
            return;
        }

        var lanes = WorkItemStore.LoadConfig(rootPath);
        var targetLane = lanes.FirstOrDefault(l =>
            l.Name.Equals(targetFolder, StringComparison.OrdinalIgnoreCase) ||
            l.Name.Replace(" ", "").Equals(targetFolder.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));

        if (targetLane == null)
        {
            var valid = string.Join(", ", lanes.Select(l => l.Name));
            Console.WriteLine($"Error: Target folder '{targetFolder}' is invalid. Valid: {valid}");
            return;
        }

        var normalizedTarget = targetLane.Name;

        if (item.Status == normalizedTarget)
        {
            Console.WriteLine($"Item is already in '{normalizedTarget}'.");
            return;
        }

        if (!overrideWip && targetLane.Wip.HasValue)
        {
            var count = items.Count(i => i.Status == normalizedTarget);
            if (count >= targetLane.Wip.Value)
            {
                Console.WriteLine($"Error: '{normalizedTarget}' is at its WIP limit ({count}/{targetLane.Wip.Value}).");
                Console.WriteLine($"Use --override-wip to proceed anyway, or move an item out first.");
                return;
            }
        }

        var sourceLane = lanes.FirstOrDefault(l => l.Name.Equals(item.Status, StringComparison.OrdinalIgnoreCase));

        // Determine destination filename
        string newFileName;
        if (!targetLane.Ordered)
        {
            // Strip number prefix when moving to an unordered lane: "42-slug.md" -> "slug.md"
            newFileName = Regex.Replace(item.FileName, @"^\d+[a-z]?-", "");
        }
        else if (sourceLane?.Ordered == false)
        {
            // Assign next safe number when promoting from an unordered lane to an ordered lane
            var maxId = items
                .Where(i => !string.IsNullOrEmpty(i.Id))
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

        // Auto-compact source folder if we moved from an ordered lane to an unordered lane
        if (!targetLane.Ordered && sourceLane?.Ordered == true)
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
        if (primaryNumbers.Count < 2)
        {
            return;
        }

        bool hasGaps = false;
        for (int i = 1; i < primaryNumbers.Count; i++)
        {
            if (primaryNumbers[i] != primaryNumbers[i - 1] + 1)
            { hasGaps = true; break; }
        }
        if (!hasGaps)
        {
            return;
        }

        Console.WriteLine($"Compacting {folder} to close gaps...");
        var orderStr = string.Join(",", primaryNumbers);
        var startNumber = primaryNumbers.Min();
        ReorderCommand.Execute(rootPath, folder, orderStr, false, false, startNumber);
    }
}
