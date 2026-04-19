using System.Text;
using System.Text.RegularExpressions;

public static class CreateCommand
{
    private const string ReservedCustomFrontmatterName = "customFrontmatter";

    private static readonly HashSet<string> ReservedFrontmatterKeys = new(StringComparer.OrdinalIgnoreCase)
        { "blocked", "tags", "dependsOn", "customFrontmatter" };

    public static void Execute(
        string rootPath, string title, string? lane, string? afterId, bool topPriority,
        bool overrideWip = false,
        IReadOnlyList<string>? initialTags = null,
        IReadOnlyDictionary<string, string>? setFields = null)
    {
        WorkItemStore.EnsureLaneDirectories(rootPath);
        var settings = WorkItemStore.LoadSettings(rootPath);
        if (!SlugHelper.IsValidCasing(settings.SlugCasing))
        {
            Console.Error.WriteLine($"Error: Invalid slug casing '{settings.SlugCasing}' in markban.json. Valid values: kebab, snake, camel, pascal.");
            return;
        }

        if (!settings.TagsEnabled && initialTags?.Count > 0)
        {
            Console.Error.WriteLine("Error: The 'tags' feature is disabled in your config. Remove --tags or enable 'tags' in markban.json.");
            return;
        }

        if (setFields != null)
        {
            var reservedKey = setFields.Keys.FirstOrDefault(k => ReservedFrontmatterKeys.Contains(k));
            if (reservedKey != null)
            {
                Console.Error.WriteLine($"Error: '--set {reservedKey}=...' is not allowed. '{reservedKey}' is a reserved key managed by markban.");
                return;
            }
        }

        var lanes = WorkItemStore.LoadConfig(rootPath);
        var resolvedLane = lane != null
            ? lanes.FirstOrDefault(l =>
                l.Name.Equals(lane, StringComparison.OrdinalIgnoreCase) ||
                l.Name.Replace(" ", "").Equals(lane.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
            : lanes.FirstOrDefault(l => l.Type == "ready");

        if (resolvedLane == null)
        {
            if (lane != null)
            {
                Console.WriteLine($"Error: Lane '{lane}' is invalid. Valid lanes: {string.Join(", ", lanes.Select(l => l.Name))}");
            }
            else
            {
                Console.WriteLine("Error: No lane with type 'ready' configured. Add \"type\": \"ready\" to a lane in markban.json.");
            }

            return;
        }

        var normalizedLane = resolvedLane.Name;

        var allItems = WorkItemStore.LoadAll(rootPath);
        var slug = SlugHelper.Generate(title, settings.SlugCasing);

        // Check for duplicate slug
        if (allItems.Any(i => i.Slug == slug))
        {
            Console.WriteLine($"Error: A work item with slug '{slug}' already exists.");
            return;
        }

        // WIP check
        if (!overrideWip && resolvedLane.Wip.HasValue)
        {
            var wipCount = allItems.Count(i => i.Status == normalizedLane);
            if (wipCount >= resolvedLane.Wip.Value)
            {
                Console.WriteLine($"Error: '{normalizedLane}' is at its WIP limit ({wipCount}/{resolvedLane.Wip.Value}).");
                Console.WriteLine("Use --override-wip to proceed anyway, or move an item out first.");
                return;
            }
        }

        string newId = "";
        if (resolvedLane.Ordered)
        {
            var laneItems = allItems.Where(i => i.Status == normalizedLane).ToList();
            int targetNumber;

            if (topPriority)
            {
                targetNumber = 1;
            }
            else if (!string.IsNullOrEmpty(afterId))
            {
                var afterItem = laneItems.FirstOrDefault(i => i.Id == afterId);
                if (afterItem == null)
                {
                    Console.WriteLine($"Error: Item with ID '{afterId}' not found in {normalizedLane}.");
                    return;
                }
                var m = Regex.Match(afterItem.Id, @"^(\d+)");
                targetNumber = int.Parse(m.Groups[1].Value) + 1;
            }
            else
            {
                // Default: Append to end
                var maxId = allItems
                    .Select(i => { var m = Regex.Match(i.Id, @"^(\d+)"); return m.Success ? int.Parse(m.Groups[1].Value) : 0; })
                    .DefaultIfEmpty(0)
                    .Max();
                targetNumber = maxId + 1;
            }

            // Shift existing items up if we are inserting
            if (topPriority || !string.IsNullOrEmpty(afterId))
            {
                ShiftIdsUp(rootPath, normalizedLane, targetNumber);
                // Refresh items after shift
                allItems = WorkItemStore.LoadAll(rootPath);
            }

            newId = targetNumber.ToString();
        }

        var fileName = string.IsNullOrEmpty(newId) ? $"{slug}.md" : $"{newId}-{slug}.md";
        var filePath = Path.Combine(rootPath, normalizedLane, fileName);

        var bodyContent = new StringBuilder();
        if (settings.HeadingEnabled)
        {
            bodyContent.AppendLine($"# {(string.IsNullOrEmpty(newId) ? "" : newId + " - ")}{title}");
            bodyContent.AppendLine();
        }
        bodyContent.Append(GetTemplateBody(rootPath));

        var fileContent = BuildContentWithFrontmatter(
            bodyContent.ToString(), settings, initialTags, setFields);

        File.WriteAllText(filePath, fileContent, new UTF8Encoding(false));

        Console.WriteLine($"Successfully created '{fileName}' in {normalizedLane}.");

        // Final sanitize to fix any potential refs
        SanitizeCommand.Execute(rootPath, WorkItemStore.LoadAll(rootPath));
    }

    public static void ExecuteSubItem(string rootPath, string title, string parentId, string? lane, string? afterId, bool overrideWip = false)
    {
        var settings = WorkItemStore.LoadSettings(rootPath);
        if (!SlugHelper.IsValidCasing(settings.SlugCasing))
        {
            Console.Error.WriteLine($"Error: Invalid slug casing '{settings.SlugCasing}' in markban.json. Valid values: kebab, snake, camel, pascal.");
            return;
        }

        var parentMatch = Regex.Match(parentId, @"^(\d+)$");
        if (!parentMatch.Success)
        {
            Console.WriteLine($"Error: --parent must be a numeric work item ID (e.g. 102), got '{parentId}'.");
            return;
        }
        var parentNumber = int.Parse(parentMatch.Groups[1].Value);

        var allItems = WorkItemStore.LoadAll(rootPath);

        // Find existing sub-items for this parent across all folders
        var subItems = allItems
            .Select(i =>
            {
                var m = Regex.Match(i.Id, @"^(\d+)([a-z])$");
                return m.Success ? (Item: i, Number: int.Parse(m.Groups[1].Value), Letter: m.Groups[2].Value[0]) : default;
            })
            .Where(x => x.Item != null && x.Number == parentNumber)
            .OrderBy(x => x.Letter)
            .ToList();

        var slug = SlugHelper.Generate(title, settings.SlugCasing);

        if (allItems.Any(i => i.Slug == slug))
        {
            Console.WriteLine($"Error: A work item with slug '{slug}' already exists.");
            return;
        }

        char newLetter;

        if (!string.IsNullOrEmpty(afterId))
        {
            // Insert after a specific sub-item
            var afterMatch = Regex.Match(afterId, @"^(\d+)([a-z])$");
            if (!afterMatch.Success)
            {
                Console.WriteLine($"Error: --after for sub-items must be a sub-item ID (e.g. 102b), got '{afterId}'.");
                return;
            }
            var afterNumber = int.Parse(afterMatch.Groups[1].Value);
            if (afterNumber != parentNumber)
            {
                Console.WriteLine($"Error: --after ID {afterId} does not belong to parent {parentNumber}.");
                return;
            }
            var afterLetter = afterMatch.Groups[2].Value[0];
            newLetter = (char)(afterLetter + 1);

            // Shift existing sub-items with letter >= newLetter (two-pass to avoid collisions)
            var toShift = subItems.Where(x => x.Letter >= newLetter).OrderByDescending(x => x.Letter).ToList();
            if (toShift.Count > 0)
            {
                if ((char)(toShift.First().Letter + 1) > 'z')
                {
                    Console.WriteLine("Error: Cannot shift sub-items beyond 'z'.");
                    return;
                }

                Console.WriteLine($"Shifting {toShift.Count} sub-item(s) to make room...");
                var tempFiles = new List<(string TempPath, string FinalPath, char NewLetter, WorkItem Item)>();

                foreach (var entry in toShift)
                {
                    var shiftedLetter = (char)(entry.Letter + 1);
                    var folderPath = Path.GetDirectoryName(entry.Item.FullPath)!;
                    var tempName = $"__sub_tmp_{Path.GetRandomFileName().Replace(".", "")}-{entry.Item.FileName}";
                    var tempPath = Path.Combine(folderPath, tempName);
                    File.Move(entry.Item.FullPath, tempPath);

                    var newFileName = $"{parentNumber}{shiftedLetter}-{entry.Item.Slug}.md";
                    var finalPath = Path.Combine(folderPath, newFileName);
                    tempFiles.Add((tempPath, finalPath, shiftedLetter, entry.Item));
                }

                foreach (var (tempPath, finalPath, shiftedLetter, _) in tempFiles)
                {
                    File.Move(tempPath, finalPath);

                    var lines = File.ReadAllLines(finalPath, Encoding.UTF8);
                    if (lines.Length > 0 && Regex.IsMatch(lines[0], @"^# \d+[a-z]? - .+$"))
                    {
                        var titleMatch = Regex.Match(lines[0], @"^# \d+[a-z]? - (.+)$");
                        lines[0] = $"# {parentNumber}{shiftedLetter} - {titleMatch.Groups[1].Value}";
                        File.WriteAllLines(finalPath, lines, new UTF8Encoding(false));
                    }
                }
            }
        }
        else
        {
            // Append as last sub-item
            newLetter = subItems.Count > 0 ? (char)(subItems.Last().Letter + 1) : 'a';
        }

        if (newLetter > 'z')
        {
            Console.WriteLine("Error: Parent item already has 26 sub-items (a-z limit reached).");
            return;
        }

        // Determine target lane
        var lanes = WorkItemStore.LoadConfig(rootPath);
        var resolvedLane = lane != null
            ? lanes.FirstOrDefault(l =>
                l.Name.Equals(lane, StringComparison.OrdinalIgnoreCase) ||
                l.Name.Replace(" ", "").Equals(lane.Replace(" ", ""), StringComparison.OrdinalIgnoreCase))
            : lanes.FirstOrDefault(l => l.Type == "ready");

        if (resolvedLane == null)
        {
            if (lane != null)
            {
                Console.WriteLine($"Error: Lane '{lane}' is invalid. Valid lanes: {string.Join(", ", lanes.Select(l => l.Name))}");
            }
            else
            {
                Console.WriteLine("Error: No lane with type 'ready' configured. Add \"type\": \"ready\" to a lane in markban.json.");
            }

            return;
        }

        var normalizedLane = resolvedLane.Name;

        // WIP check
        if (!overrideWip && resolvedLane.Wip.HasValue)
        {
            var wipCount = allItems.Count(i => i.Status == normalizedLane);
            if (wipCount >= resolvedLane.Wip.Value)
            {
                Console.WriteLine($"Error: '{normalizedLane}' is at its WIP limit ({wipCount}/{resolvedLane.Wip.Value}).");
                Console.WriteLine("Use --override-wip to proceed anyway, or move an item out first.");
                return;
            }
        }

        var newId = $"{parentNumber}{newLetter}";
        var fileName = $"{newId}-{slug}.md";
        var filePath = Path.Combine(rootPath, normalizedLane, fileName);

        var bodyContent = new StringBuilder();
        if (settings.HeadingEnabled)
        {
            bodyContent.AppendLine($"# {newId} - {title}");
            bodyContent.AppendLine();
        }
        bodyContent.Append(GetTemplateBody(rootPath));

        var fileContent = BuildContentWithFrontmatter(bodyContent.ToString(), settings, null, null);
        File.WriteAllText(filePath, fileContent, new UTF8Encoding(false));

        Console.WriteLine($"Successfully created sub-item '{fileName}' in {normalizedLane}.");

        SanitizeCommand.Execute(rootPath, WorkItemStore.LoadAll(rootPath));
    }

    private static string BuildContentWithFrontmatter(
        string body,
        BoardSettings settings,
        IReadOnlyList<string>? initialTags,
        IReadOnlyDictionary<string, string>? setFields)
    {
        var (fields, existingBody) = FrontmatterParser.Parse(body);

        if (settings.TagsEnabled && initialTags?.Count > 0)
        {
            fields["tags"] = initialTags.ToList();
        }

        if (settings.BlockedEnabled && !fields.ContainsKey("blocked"))
        {
            // no default value — omitted unless set
        }

        foreach (var customField in settings.CustomFrontmatter ?? [])
        {
            if (setFields != null && setFields.TryGetValue(customField.Name, out var override_))
            {
                fields[customField.Name] = override_;
            }
            else if (customField.HasDefault)
            {
                // When Default is null (JSON "default": null), store object null so SerializeField writes YAML null
                fields[customField.Name] = (object?)customField.Default;
            }
        }

        if (setFields != null)
        {
            foreach (var kvp in setFields)
            {
                if (ReservedFrontmatterKeys.Contains(kvp.Key))
                {
                    Console.Error.WriteLine($"Warning: '--set {kvp.Key}=...' ignored — '{kvp.Key}' is managed by markban. Use the dedicated command instead.");
                    continue;
                }

                if (!(settings.CustomFrontmatter ?? []).Any(f => f.Name == kvp.Key))
                {
                    fields[kvp.Key] = kvp.Value;
                }
            }
        }

        return FrontmatterParser.BuildContent(fields, existingBody);
    }

    private static string GetTemplateBody(string rootPath)
    {
        var customTemplate = Path.Combine(rootPath, ".template.md");
        if (File.Exists(customTemplate))
        {
            return File.ReadAllText(customTemplate, new UTF8Encoding(false));
        }

        return """  
            ## Description

            Describe the goal and context of this task.

            ---

            ## Acceptance Criteria

            - [ ] Criterion 1

            """;
    }

    internal static void ShiftIdsUp(string rootPath, string folder, int startingAt)
    {
        var allItems = WorkItemStore.LoadAll(rootPath);
        var laneItems = allItems
            .Where(i => i.Status == folder)
            .Select(i =>
            {
                var m = Regex.Match(i.Id, @"^(\d+)([a-z]?)$");
                return (Item: i, Number: int.Parse(m.Groups[1].Value), Letter: m.Groups[2].Value);
            })
            .Where(x => x.Number >= startingAt)
            .OrderByDescending(x => x.Number).ThenByDescending(x => x.Letter)
            .ToList();

        if (laneItems.Count == 0)
        {
            return;
        }

        Console.WriteLine($"Shifting {laneItems.Count} items in {folder} to make room...");

        // Similar two-pass rename logic as ReorderCommand
        var folderPath = Path.Combine(rootPath, folder);
        var tempMap = new Dictionary<string, string>();

        foreach (var entry in laneItems)
        {
            var newNumber = entry.Number + 1;
            var newName = $"{newNumber}{entry.Letter}-{entry.Item.Slug}.md";
            var tempName = $"__shift_tmp_{Path.GetRandomFileName().Replace(".", "")}-{entry.Item.FileName}";
            var tempPath = Path.Combine(folderPath, tempName);

            File.Move(entry.Item.FullPath, tempPath);
            tempMap[newName] = tempPath;
        }

        foreach (var kvp in tempMap)
        {
            var finalPath = Path.Combine(folderPath, kvp.Key);
            File.Move(kvp.Value, finalPath);

            // Update header (skips frontmatter if present)
            var lines = File.ReadAllLines(finalPath, Encoding.UTF8);
            var headingIdx = FindHeadingLine(lines);
            if (headingIdx >= 0 && Regex.IsMatch(lines[headingIdx], @"^# \d+[a-z]? - .+$"))
            {
                var m = Regex.Match(kvp.Key, @"^(\d+[a-z]?)");
                var newHeadingId = m.Groups[1].Value;
                var titleMatch = Regex.Match(lines[headingIdx], @"^# \d+[a-z]? - (.+)$");
                lines[headingIdx] = $"# {newHeadingId} - {titleMatch.Groups[1].Value}";
                File.WriteAllLines(finalPath, lines, new UTF8Encoding(false));
            }
        }
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
