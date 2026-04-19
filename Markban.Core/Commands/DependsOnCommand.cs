using System.Text;

/// <summary>Commands for managing item dependencies via frontmatter.</summary>
public static class DependsOnCommand
{
    private const string DepsField = "dependsOn";

    public static void AddDependency(string rootPath, string identifier, string dependencySlug)
    {
        var settings = WorkItemStore.LoadSettings(rootPath);
        if (!settings.DependsOnEnabled)
        {
            Console.Error.WriteLine("Error: The 'dependsOn' feature is disabled in your config.");
            return;
        }

        var item = FindItem(rootPath, identifier);
        if (item == null)
        {
            return;
        }

        var content = File.ReadAllText(item.FullPath, new UTF8Encoding(false));
        var existing = FrontmatterParser.GetListField(content, DepsField) ?? [];
        if (existing.Contains(dependencySlug, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"'{dependencySlug}' is already a dependency of '{item.Slug}'.");
            return;
        }

        var updated = existing.Concat([dependencySlug]).ToList();
        var newContent = FrontmatterParser.SetField(content, DepsField, updated);
        File.WriteAllText(item.FullPath, newContent, new UTF8Encoding(false));

        Console.WriteLine($"Added dependency: '{item.Slug}' depends on '{dependencySlug}'.");
    }

    public static void RemoveDependency(string rootPath, string identifier, string dependencySlug)
    {
        var settings = WorkItemStore.LoadSettings(rootPath);
        if (!settings.DependsOnEnabled)
        {
            Console.Error.WriteLine("Error: The 'dependsOn' feature is disabled in your config.");
            return;
        }

        var item = FindItem(rootPath, identifier);
        if (item == null)
        {
            return;
        }

        var content = File.ReadAllText(item.FullPath, new UTF8Encoding(false));
        var existing = FrontmatterParser.GetListField(content, DepsField) ?? [];
        var updated = existing.Where(s => !s.Equals(dependencySlug, StringComparison.OrdinalIgnoreCase)).ToList();

        var newContent = updated.Count > 0
            ? FrontmatterParser.SetField(content, DepsField, updated)
            : FrontmatterParser.RemoveField(content, DepsField);

        File.WriteAllText(item.FullPath, newContent, new UTF8Encoding(false));
        Console.WriteLine($"Removed dependency '{dependencySlug}' from '{item.Slug}'.");
    }

    public static void ShowDependencies(string rootPath, string identifier)
    {
        var settings = WorkItemStore.LoadSettings(rootPath);
        if (!settings.DependsOnEnabled)
        {
            Console.Error.WriteLine("Error: The 'dependsOn' feature is disabled in your config.");
            return;
        }

        var items = WorkItemStore.LoadAll(rootPath);
        var item = items.FirstOrDefault(i =>
            i.Id.Equals(identifier, StringComparison.OrdinalIgnoreCase) ||
            i.Slug.Equals(identifier, StringComparison.OrdinalIgnoreCase));

        if (item == null)
        {
            Console.Error.WriteLine($"Error: Work item '{identifier}' not found.");
            return;
        }

        var deps = FrontmatterParser.GetListField(item.Content, DepsField);
        if (deps == null || deps.Count == 0)
        {
            Console.WriteLine($"'{item.Slug}' has no dependencies.");
            return;
        }

        var lanes = WorkItemStore.LoadConfig(rootPath);
        var doneLane = lanes.FirstOrDefault(l => l.Type == "done")?.Name;
        var slugToItem = items.ToDictionary(i => i.Slug, i => i, StringComparer.OrdinalIgnoreCase);

        Console.WriteLine($"Dependencies of '{item.Slug}':");
        foreach (var dep in deps)
        {
            if (slugToItem.TryGetValue(dep, out var depItem))
            {
                var resolved = doneLane != null && depItem.Status == doneLane;
                var status = resolved ? "done" : $"pending ({depItem.Status})";
                Console.WriteLine($"  [{dep}] — {status}");
            }
            else
            {
                Console.WriteLine($"  [{dep}] — not found (broken reference)");
            }
        }
    }

    public static void ListUnresolved(string rootPath)
    {
        var settings = WorkItemStore.LoadSettings(rootPath);
        if (!settings.DependsOnEnabled)
        {
            Console.Error.WriteLine("Error: The 'dependsOn' feature is disabled in your config.");
            return;
        }

        var items = WorkItemStore.LoadAll(rootPath);
        var lanes = WorkItemStore.LoadConfig(rootPath);
        var doneLane = lanes.FirstOrDefault(l => l.Type == "done")?.Name;
        var doneSlugs = doneLane != null
            ? items.Where(i => i.Status == doneLane).Select(i => i.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var blocked = items
            .Select(i =>
            {
                var deps = FrontmatterParser.GetListField(i.Content, DepsField) ?? [];
                var unresolved = deps.Where(d => !doneSlugs.Contains(d)).ToList();
                return (Item: i, Unresolved: unresolved);
            })
            .Where(x => x.Unresolved.Count > 0)
            .ToList();

        if (blocked.Count == 0)
        {
            Console.WriteLine("No items with unresolved dependencies.");
            return;
        }

        Console.WriteLine($"{blocked.Count} item(s) with unresolved dependencies:");
        foreach (var (blockedItem, unresolved) in blocked)
        {
            var id = string.IsNullOrEmpty(blockedItem.Id) ? blockedItem.Slug : blockedItem.Id;
            Console.WriteLine($"  [{id}] {blockedItem.Slug} ({blockedItem.Status})");
            foreach (var dep in unresolved)
            {
                Console.WriteLine($"    -> depends on [{dep}]");
            }
        }
    }

    /// <summary>Returns the dependsOn slugs for an item's content.</summary>
    public static List<string> GetDependencies(string content)
        => FrontmatterParser.GetListField(content, DepsField) ?? [];

    /// <summary>Detects circular dependencies in the loaded items list.</summary>
    public static List<string> FindCircularDependencies(List<WorkItem> items)
    {
        var slugToItem = items.ToDictionary(i => i.Slug, i => i, StringComparer.OrdinalIgnoreCase);
        var warnings = new List<string>();

        foreach (var item in items)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (HasCycle(item.Slug, slugToItem, visited, [item.Slug]))
            {
                warnings.Add($"Circular dependency detected involving '{item.Slug}'");
            }
        }

        return warnings.Distinct().ToList();
    }

    private static bool HasCycle(
        string slug,
        Dictionary<string, WorkItem> slugToItem,
        HashSet<string> globalVisited,
        HashSet<string> path)
    {
        if (!slugToItem.TryGetValue(slug, out var item))
        {
            return false;
        }

        var deps = FrontmatterParser.GetListField(item.Content, DepsField) ?? [];
        foreach (var dep in deps)
        {
            if (path.Contains(dep))
            {
                return true;
            }

            if (globalVisited.Contains(dep))
            {
                continue;
            }

            globalVisited.Add(dep);
            path.Add(dep);
            if (HasCycle(dep, slugToItem, globalVisited, path))
            {
                return true;
            }

            path.Remove(dep);
        }

        return false;
    }

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
