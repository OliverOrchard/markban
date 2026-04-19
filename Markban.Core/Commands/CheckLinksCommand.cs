using System.Text.RegularExpressions;

public static class CheckLinksCommand
{
    public record BrokenLink(string FileName, string Slug, int Line, List<string> Suggestions);
    public record NumericRef(string FileName, string RawRef, int Line, string? ResolvedSlug);
    public record BrokenDependency(string FileName, string DependsOnSlug, List<string> Suggestions);

    public static (List<BrokenLink> Broken, List<NumericRef> NumericRefs) Execute(string rootPath, List<WorkItem> items, bool includeIdeas = false)
    {
        var knownSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            knownSlugs.Add(item.Slug);
        }

        var foldersToScan = WorkItemStore.LoadConfig(rootPath)
            .Where(l => l.Pickable || includeIdeas)
            .Select(l => l.Name)
            .ToList();

        var broken = new List<BrokenLink>();
        var numericRefs = new List<NumericRef>();
        var slugLinkPattern = new Regex(@"\[([a-z][a-z0-9-]*(?:-[a-z0-9]+)+)\]", RegexOptions.IgnoreCase);
        // "- 012 (Customer Jobs)" style in Depends On sections
        var listNumericPattern = new Regex(@"^-\s+0*(\d+)[a-z]?\s*\(", RegexOptions.None);
        // Inline "(020)" or "(22)" parenthetical task refs in prose
        var inlineNumericPattern = new Regex(@"(?<!\[)\(0*(\d{2,3})\)", RegexOptions.None);

        // Build ID-to-slug lookup for numeric ref resolution
        var idToSlug = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (!string.IsNullOrEmpty(item.Id))
            {
                idToSlug.TryAdd(item.Id, item.Slug);
            }
        }

        foreach (var folder in foldersToScan)
        {
            var folderPath = Path.Combine(rootPath, folder);
            if (!Directory.Exists(folderPath))
            {
                continue;
            }

            foreach (var file in Directory.GetFiles(folderPath, "*.md"))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.StartsWith("."))
                {
                    continue;
                }

                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (Match match in slugLinkPattern.Matches(lines[i]))
                    {
                        var slug = match.Groups[1].Value;
                        if (!knownSlugs.Contains(slug))
                        {
                            var suggestions = FindSuggestions(slug, items);
                            broken.Add(new BrokenLink(fileName, slug, i + 1, suggestions));
                        }
                    }

                    var numMatch = listNumericPattern.Match(lines[i]);
                    if (numMatch.Success)
                    {
                        var rawId = numMatch.Groups[1].Value;
                        var rawText = lines[i].Trim();
                        idToSlug.TryGetValue(rawId, out var resolvedSlug);
                        numericRefs.Add(new NumericRef(fileName, rawText, i + 1, resolvedSlug));
                    }
                    else
                    {
                        // Only check inline pattern if line didn't match the list pattern
                        // (to avoid double-reporting "- 012 (Foo)" as both styles)
                        foreach (Match inlineMatch in inlineNumericPattern.Matches(lines[i]))
                        {
                            var rawId = inlineMatch.Groups[1].Value;
                            var rawText = lines[i].Trim();
                            idToSlug.TryGetValue(rawId, out var resolvedSlug);
                            numericRefs.Add(new NumericRef(fileName, rawText, i + 1, resolvedSlug));
                        }
                    }
                }
            }
        }

        return (broken, numericRefs);
    }

    internal static List<string> FindSuggestions(string brokenSlug, List<WorkItem> items, int maxResults = 3)
    {
        var brokenWords = brokenSlug.ToLower().Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (brokenWords.Length == 0)
        {
            return [];
        }

        return items
            .Select(item =>
            {
                var slugWords = item.Slug.ToLower().Split('-', StringSplitOptions.RemoveEmptyEntries);
                int matchedWords = brokenWords.Count(w => slugWords.Any(sw => sw.Contains(w) || w.Contains(sw)));
                double wordRatio = (double)matchedWords / Math.Max(brokenWords.Length, 1);

                // Bonus for substring containment in either direction
                int substringBonus = 0;
                if (item.Slug.Contains(brokenSlug, StringComparison.OrdinalIgnoreCase) ||
                    brokenSlug.Contains(item.Slug, StringComparison.OrdinalIgnoreCase))
                {
                    substringBonus = 100;
                }

                // Score: weighted word overlap + substring bonus
                int score = (int)(wordRatio * 200) + substringBonus;

                // Minimum threshold: at least 1 matching word for short slugs, 2 for longer ones
                int minWords = brokenWords.Length <= 2 ? 1 : 2;
                if (matchedWords < minWords && substringBonus == 0)
                {
                    score = 0;
                }

                return new { item.Slug, item.Id, item.Status, Score = score };
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(maxResults)
            .Select(x => string.IsNullOrEmpty(x.Id) ? $"{x.Slug} ({x.Status})" : $"{x.Id}: {x.Slug} ({x.Status})")
            .ToList();
    }

    public static List<BrokenDependency> ValidateDependsOn(List<WorkItem> items)
    {
        var knownSlugs = items.Select(i => i.Slug).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var broken = new List<BrokenDependency>();

        foreach (var item in items)
        {
            var deps = DependsOnCommand.GetDependencies(item.Content);
            foreach (var dep in deps)
            {
                if (!knownSlugs.Contains(dep))
                {
                    broken.Add(new BrokenDependency(item.FileName, dep, FindSuggestions(dep, items)));
                }
            }
        }

        return broken;
    }

    public static void PrintResults(List<BrokenLink> broken, List<NumericRef> numericRefs,
        List<BrokenDependency>? brokenDeps = null)
    {
        if (broken.Count == 0 && numericRefs.Count == 0 && (brokenDeps == null || brokenDeps.Count == 0))
        {
            Console.WriteLine("No broken links found.");
            return;
        }

        if (broken.Count > 0)
        {
            Console.WriteLine($"Found {broken.Count} broken link(s):\n");
            var grouped = broken.GroupBy(b => b.FileName);
            foreach (var group in grouped)
            {
                Console.WriteLine($"  {group.Key}:");
                foreach (var link in group)
                {
                    Console.WriteLine($"    Line {link.Line}: [{link.Slug}]");
                    if (link.Suggestions.Count > 0)
                    {
                        foreach (var suggestion in link.Suggestions)
                        {
                            Console.WriteLine($"      -> Did you mean: {suggestion}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"      -> No potential matches found");
                    }
                }
            }
        }

        if (numericRefs.Count > 0)
        {
            Console.WriteLine($"\nFound {numericRefs.Count} bare numeric reference(s) (should use [slug] format):\n");
            var grouped = numericRefs.GroupBy(n => n.FileName);
            foreach (var group in grouped)
            {
                Console.WriteLine($"  {group.Key}:");
                foreach (var nr in group)
                {
                    Console.WriteLine($"    Line {nr.Line}: {nr.RawRef}");
                    if (nr.ResolvedSlug != null)
                    {
                        Console.WriteLine($"      -> Convert to: [{nr.ResolvedSlug}]");
                    }
                    else
                    {
                        Console.WriteLine($"      -> Could not resolve to a known work item");
                    }
                }
            }
        }

        if (brokenDeps != null && brokenDeps.Count > 0)
        {
            Console.WriteLine($"\nFound {brokenDeps.Count} broken dependsOn reference(s):\n");
            var grouped = brokenDeps.GroupBy(d => d.FileName);
            foreach (var group in grouped)
            {
                Console.WriteLine($"  {group.Key}:");
                foreach (var dep in group)
                {
                    Console.WriteLine($"    dependsOn: [{dep.DependsOnSlug}]");
                    if (dep.Suggestions.Count > 0)
                    {
                        foreach (var suggestion in dep.Suggestions)
                        {
                            Console.WriteLine($"      -> Did you mean: {suggestion}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"      -> No potential matches found");
                    }
                }
            }
        }
    }
}
