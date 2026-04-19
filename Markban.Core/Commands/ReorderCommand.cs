using System.Text;
using System.Text.RegularExpressions;

public static class ReorderCommand
{
    public static void Execute(string rootPath, string folder, string orderArg, bool noSubItems, bool dryRun, int? startNumber = null)
    {
        var lanes = WorkItemStore.LoadConfig(rootPath);
        var targetLane = lanes.FirstOrDefault(l =>
            l.Name.Equals(folder, StringComparison.OrdinalIgnoreCase) ||
            l.Name.Replace(" ", "").Equals(folder.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));

        if (targetLane == null)
        {
            var validOrdered = string.Join(", ", lanes.Where(l => l.Ordered).Select(l => l.Name));
            Console.WriteLine($"Error: Folder '{folder}' is invalid. Valid ordered lanes: {validOrdered}");
            return;
        }

        if (!targetLane.Ordered)
        {
            Console.WriteLine($"Error: Lane '{targetLane.Name}' is not an ordered lane and cannot be reordered.");
            return;
        }

        var normalizedFolder = targetLane.Name;

        var allItems = WorkItemStore.LoadAll(rootPath);

        var folderItems = allItems
            .Where(i => i.Status == normalizedFolder)
            .Select(i =>
            {
                var m = Regex.Match(i.Id, @"^(\d+)([a-z]?)$");
                return (Item: i, Number: int.Parse(m.Groups[1].Value), Letter: m.Groups[2].Value);
            })
            .OrderBy(x => x.Number).ThenBy(x => x.Letter)
            .ToList();

        var primaryItems = folderItems.Where(x => x.Letter == "").ToList();
        var subItems = folderItems.Where(x => x.Letter != "").ToList();

        if (primaryItems.Count == 0)
        {
            Console.WriteLine($"No work items found in: {normalizedFolder}");
            return;
        }

        var requestedNumbers = orderArg
            .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(s => int.TryParse(s.Trim(), out _))
            .Select(s => int.Parse(s.Trim()))
            .ToList();

        if (requestedNumbers.Count == 0)
        {
            Console.WriteLine("Error: <order> must be comma-separated item numbers, e.g. 97,98,99");
            Console.WriteLine($"Current items in {normalizedFolder}:");
            foreach (var p in primaryItems)
            {
                Console.WriteLine($"  [{p.Number}] {p.Item.Slug}");
            }
            return;
        }

        var existingNumbers = primaryItems.Select(x => x.Number).ToHashSet();
        foreach (var n in requestedNumbers)
        {
            if (!existingNumbers.Contains(n))
            {
                Console.WriteLine($"Error: item {n} not found in {normalizedFolder}.");
                Console.WriteLine($"Available: {string.Join(", ", existingNumbers.Order())}");
                return;
            }
        }

        var mentioned = requestedNumbers.ToHashSet();
        var remainder = primaryItems.Where(x => !mentioned.Contains(x.Number)).ToList();
        var orderedPrimary = requestedNumbers
            .Select(n => primaryItems.First(x => x.Number == n))
            .Concat(remainder)
            .ToList();

        int baseNumber;
        if (startNumber.HasValue)
        {
            baseNumber = startNumber.Value;
        }
        else
        {
            var maxOtherNumber = allItems
                .Where(i => i.Status != normalizedFolder)
                .Select(i => { var m = Regex.Match(i.Id, @"^(\d+)"); return m.Success ? int.Parse(m.Groups[1].Value) : 0; })
                .DefaultIfEmpty(0)
                .Max();
            baseNumber = maxOtherNumber + 1;
        }

        // Build rename plan
        var renamePlan = new List<(string OldPath, string OldName, string NewName, string NewHeadingId, bool Changed)>();
        var numberMap = new Dictionary<int, int>();

        for (var i = 0; i < orderedPrimary.Count; i++)
        {
            var (item, oldNumber, _) = orderedPrimary[i];
            var newNumber = baseNumber + i;
            numberMap[oldNumber] = newNumber;
            var newName = $"{newNumber}-{item.Slug}.md";
            renamePlan.Add((item.FullPath, item.FileName, newName, $"{newNumber}", item.FileName != newName));
        }

        if (!noSubItems)
        {
            foreach (var (item, oldNumber, letter) in subItems)
            {
                if (numberMap.TryGetValue(oldNumber, out var newNumber))
                {
                    var newName = $"{newNumber}{letter}-{item.Slug}.md";
                    renamePlan.Add((item.FullPath, item.FileName, newName, $"{newNumber}{letter}", item.FileName != newName));
                }
            }
        }
        else if (subItems.Count > 0)
        {
            Console.WriteLine($"Note: {subItems.Count} lettered sub-item(s) excluded from reorder (--no-sub-items):");
            foreach (var (item, _, _) in subItems)
            {
                Console.WriteLine($"  {item.FileName}");
            }
        }

        Console.WriteLine($"\nReorder plan for: {normalizedFolder}");
        var anyChanges = false;
        foreach (var entry in renamePlan)
        {
            if (entry.Changed)
            {
                anyChanges = true;
                Console.WriteLine($"  {entry.OldName,-55} -> {entry.NewName}");
            }
            else
            {
                Console.WriteLine($"  {entry.OldName,-55}   unchanged");
            }
        }

        if (!anyChanges)
        {
            Console.WriteLine("\nNothing to rename - already in the correct order.");
            return;
        }

        if (dryRun)
        {
            Console.WriteLine("\nDry run: no files were renamed.");
            return;
        }

        // Two-pass rename to avoid collisions (e.g. 34->35 when 35 still exists)
        var folderPath = Path.Combine(rootPath, normalizedFolder);
        var tempMap = new Dictionary<string, string>();

        foreach (var entry in renamePlan.Where(e => e.Changed))
        {
            var tempName = $"__tmp_{Path.GetRandomFileName().Replace(".", "")}-{entry.OldName}";
            var tempPath = Path.Combine(folderPath, tempName);
            File.Move(entry.OldPath, tempPath);
            tempMap[entry.NewName] = tempPath;
        }

        foreach (var entry in renamePlan.Where(e => e.Changed))
        {
            File.Move(tempMap[entry.NewName], Path.Combine(folderPath, entry.NewName));
        }

        // Update "# N[a] - Title" heading in each renamed file
        var headingsUpdated = 0;
        foreach (var entry in renamePlan.Where(e => e.Changed))
        {
            var finalPath = Path.Combine(folderPath, entry.NewName);
            var lines = File.ReadAllLines(finalPath, Encoding.UTF8);
            var headingIdx = FindHeadingLine(lines);
            if (headingIdx >= 0 && Regex.IsMatch(lines[headingIdx], @"^# \d+[a-z]? - .+$"))
            {
                var titleMatch = Regex.Match(lines[headingIdx], @"^# \d+[a-z]? - (.+)$");
                lines[headingIdx] = $"# {entry.NewHeadingId} - {titleMatch.Groups[1].Value}";
                File.WriteAllLines(finalPath, lines, new UTF8Encoding(false));
                headingsUpdated++;
            }
        }

        var changed = renamePlan.Count(e => e.Changed);
        Console.WriteLine($"\n{changed} file(s) renamed, {headingsUpdated} heading(s) updated.");
    }

    /// <summary>
    /// Returns the index of the first H1 heading line, skipping any frontmatter block.
    /// Returns -1 if no heading is found.
    /// </summary>
    private static int FindHeadingLine(string[] lines)
    {
        var i = 0;

        // Skip frontmatter block (--- ... ---)
        if (lines.Length > 0 && lines[0].TrimEnd() == "---")
        {
            i = 1;
            while (i < lines.Length && lines[i].TrimEnd() != "---")
            {
                i++;
            }

            i++; // step past closing ---
        }

        while (i < lines.Length)
        {
            if (lines[i].StartsWith("# "))
            {
                return i;
            }

            i++;
        }

        return -1;
    }
}
