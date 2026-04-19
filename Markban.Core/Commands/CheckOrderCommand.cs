using System.Text.RegularExpressions;

public static class CheckOrderCommand
{
    public static (bool HasIssues, List<string> Messages) Execute(string rootPath, bool fix)
    {
        var items = WorkItemStore.LoadAll(rootPath);
        var settings = WorkItemStore.LoadSettings(rootPath);
        var messages = new List<string>();

        CheckDuplicatesAndGaps(items, messages);

        if (settings.DependsOnEnabled)
        {
            CheckDependencyOrder(rootPath, items, messages);
        }

        if (fix && messages.Count > 0 && settings.DependsOnEnabled)
        {
            AutoFixViolations(rootPath, items, messages);
        }

        return (messages.Count > 0, messages);
    }

    private static void AutoFixViolations(string rootPath, List<WorkItem> items, List<string> messages)
    {
        var lanes = WorkItemStore.LoadConfig(rootPath);
        var doneLane = lanes.FirstOrDefault(l => l.Type == "done")?.Name;

        foreach (var lane in lanes.Where(l => l.Ordered && l.Name != doneLane))
        {
            var laneItems = items
                .Where(i => i.Status == lane.Name && Regex.IsMatch(i.Id, @"^\d+$"))
                .OrderBy(i => int.Parse(i.Id))
                .ToList();

            if (laneItems.Count < 2)
            {
                continue;
            }

            var slugToItem = laneItems.ToDictionary(i => i.Slug, i => i, StringComparer.OrdinalIgnoreCase);
            var depsMap = laneItems.ToDictionary(
                i => i.Slug,
                i => DependsOnCommand.GetDependencies(i.Content)
                     .Where(d => slugToItem.ContainsKey(d))
                     .ToList(),
                StringComparer.OrdinalIgnoreCase);

            var hasViolation = laneItems.Any(item =>
            {
                var itemId = int.Parse(item.Id);
                return depsMap[item.Slug].Any(dep =>
                    slugToItem.TryGetValue(dep, out var depItem) && int.Parse(depItem.Id) > itemId);
            });

            if (!hasViolation)
            {
                continue;
            }

            var sorted = TopologicalSort(laneItems, depsMap, slugToItem);
            if (sorted == null)
            {
                messages.Add($"  --fix: '{lane.Name}' has circular dependencies — skipped.");
                continue;
            }

            var newOrder = string.Join(",", sorted.Select(i => i.Id));
            messages.Add($"  --fix: Reordered '{lane.Name}' to resolve dependency violations.");
            ReorderCommand.Execute(rootPath, lane.Name, newOrder, noSubItems: true, dryRun: false);
        }
    }

    private static List<WorkItem>? TopologicalSort(
        List<WorkItem> items,
        Dictionary<string, List<string>> depsMap,
        Dictionary<string, WorkItem> slugToItem)
    {
        var remaining = new List<WorkItem>(items);
        var result = new List<WorkItem>();
        var done = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (remaining.Count > 0)
        {
            var ready = remaining
                .Where(item => depsMap[item.Slug].All(d => !slugToItem.ContainsKey(d) || done.Contains(d)))
                .OrderBy(i => items.IndexOf(i))
                .FirstOrDefault();

            if (ready == null)
            {
                return null; // cycle
            }

            result.Add(ready);
            done.Add(ready.Slug);
            remaining.Remove(ready);
        }

        return result;
    }

    private static void CheckDuplicatesAndGaps(List<WorkItem> items, List<string> messages)
    {
        var duplicates = items
            .Where(i => !string.IsNullOrEmpty(i.Id))
            .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);

        foreach (var dup in duplicates)
        {
            messages.Add($"Duplicate ID '{dup.Key}': {string.Join(", ", dup.Select(i => i.FileName))}");
        }

        var primaryIds = items
            .Where(i => Regex.IsMatch(i.Id, @"^\d+$"))
            .Select(i => int.Parse(i.Id))
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        if (primaryIds.Count > 0)
        {
            for (var n = primaryIds[0] + 1; n < primaryIds[^1]; n++)
            {
                if (!primaryIds.Contains(n))
                {
                    messages.Add($"Gap in global ID sequence: {n} is missing");
                }
            }
        }
    }

    private static void CheckDependencyOrder(string rootPath, List<WorkItem> items, List<string> messages)
    {
        var lanes = WorkItemStore.LoadConfig(rootPath);
        var doneLane = lanes.FirstOrDefault(l => l.Type == "done")?.Name;

        // Build slug -> (numericId, item) lookup
        var slugToInfo = items
            .Select(i =>
            {
                var m = Regex.Match(i.Id, @"^(\d+)");
                return m.Success
                    ? (Slug: i.Slug, NumericId: int.Parse(m.Groups[1].Value), Item: i)
                    : (Slug: i.Slug, NumericId: -1, Item: i);
            })
            .Where(x => !string.IsNullOrEmpty(x.Slug))
            .ToDictionary(x => x.Slug, x => x, StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            // Items already in done lane are always satisfied
            if (doneLane != null && item.Status == doneLane)
            {
                continue;
            }

            var m = Regex.Match(item.Id, @"^(\d+)");
            if (!m.Success)
            {
                continue;
            }

            var itemNumericId = int.Parse(m.Groups[1].Value);
            var deps = DependsOnCommand.GetDependencies(item.Content);

            foreach (var dep in deps)
            {
                if (!slugToInfo.TryGetValue(dep, out var depInfo))
                {
                    continue; // unresolvable slugs reported by check-links
                }

                // Skip if dependency is in done lane (already resolved)
                if (doneLane != null && depInfo.Item.Status == doneLane)
                {
                    continue;
                }

                if (depInfo.NumericId > itemNumericId)
                {
                    messages.Add(
                        $"{item.Id}-{item.Slug} depends on {depInfo.NumericId}-{dep} " +
                        $"({item.Id} < {depInfo.NumericId} but depends on it — ordering violation)");
                }
            }
        }

        // Circular dependency warnings
        var cycles = DependsOnCommand.FindCircularDependencies(items);
        messages.AddRange(cycles);
    }

    public static void PrintResults(bool hasIssues, List<string> messages)
    {
        if (!hasIssues)
        {
            Console.WriteLine("check-order: OK");
            return;
        }

        Console.WriteLine($"check-order: {messages.Count} issue(s) found:");
        foreach (var msg in messages)
        {
            Console.WriteLine($"  {msg}");
        }
    }
}
