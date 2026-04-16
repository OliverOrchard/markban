using System.Text.Json;
using System.Text.RegularExpressions;

public static class WorkItemStore
{
    public static string FindRoot(string? startDir = null)
    {
        var current = startDir ?? Directory.GetCurrentDirectory();
        while (current != null)
        {
            var configPath = Path.Combine(current, "markban.json");
            if (File.Exists(configPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
                    if (doc.RootElement.TryGetProperty("rootPath", out var el))
                    {
                        var relPath = el.GetString();
                        if (!string.IsNullOrWhiteSpace(relPath))
                        {
                            return Path.GetFullPath(Path.Combine(current, relPath));
                        }
                    }
                }
                catch (JsonException) { }
            }

            var potential = Path.Combine(current, "work-items");
            if (Directory.Exists(potential))
            {
                return potential;
            }

            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException("Could not find 'work-items' directory in any parent path.");
    }

    public static IReadOnlyList<LaneConfig> LoadConfig(string root)
    {
        var projectDir = Path.GetDirectoryName(root);
        if (projectDir == null)
        {
            return BoardConfig.DefaultLanes;
        }

        var configPath = Path.Combine(projectDir, "markban.json");
        if (!File.Exists(configPath))
        {
            return BoardConfig.DefaultLanes;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            if (!doc.RootElement.TryGetProperty("lanes", out var lanesEl))
            {
                return BoardConfig.DefaultLanes;
            }

            var lanes = lanesEl.EnumerateArray()
                .Select(ParseLane)
                .OfType<LaneConfig>()
                .ToList();

            return lanes.Count > 0 ? lanes : BoardConfig.DefaultLanes;
        }
        catch (JsonException)
        {
            return BoardConfig.DefaultLanes;
        }
    }

    public static BoardSettings LoadSettings(string root)
    {
        var projectDir = Path.GetDirectoryName(root);
        if (projectDir == null)
        {
            return new BoardSettings();
        }

        var configPath = Path.Combine(projectDir, "markban.json");
        if (!File.Exists(configPath))
        {
            return new BoardSettings();
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            var maxLen = 72;
            IReadOnlyList<string>? tags = null;
            var headingEnabled = true;
            var slugCasing = "kebab";

            if (doc.RootElement.TryGetProperty("commit", out var commitEl))
            {
                if (commitEl.TryGetProperty("maxMessageLength", out var maxLenEl))
                {
                    maxLen = maxLenEl.GetInt32();
                }

                if (commitEl.TryGetProperty("tags", out var tagsEl))
                {
                    tags = tagsEl.EnumerateArray()
                        .Select(e => e.GetString())
                        .OfType<string>()
                        .ToList();
                }
            }

            if (doc.RootElement.TryGetProperty("heading", out var headingEl)
                && headingEl.TryGetProperty("enabled", out var enabledEl))
            {
                headingEnabled = enabledEl.GetBoolean();
            }

            if (doc.RootElement.TryGetProperty("slugs", out var slugsEl)
                && slugsEl.TryGetProperty("casing", out var casingEl))
            {
                slugCasing = casingEl.GetString() ?? "kebab";
            }

            return new BoardSettings(maxLen, tags, headingEnabled, slugCasing);
        }
        catch (JsonException)
        {
            return new BoardSettings();
        }
    }

    public static void EnsureLaneDirectories(string root)
    {
        foreach (var lane in LoadConfig(root))
        {
            Directory.CreateDirectory(Path.Combine(root, lane.Name));
        }
    }

    public static List<WorkItem> LoadAll(string root)
    {
        var lanes = LoadConfig(root);
        var result = new List<WorkItem>();

        foreach (var lane in lanes)
        {
            var path = FindLaneDirectory(root, lane.Name);
            if (path == null)
            {
                continue;
            }

            if (lane.Ordered)
            {
                result.AddRange(LoadOrderedLane(path, lane.Name, lane.Type == "done"));
            }
            else
            {
                result.AddRange(LoadUnorderedLane(path, lane.Name));
            }
        }

        return result;
    }

    private static LaneConfig? ParseLane(JsonElement el)
    {
        if (!el.TryGetProperty("name", out var nameEl))
        {
            return null;
        }

        var name = nameEl.GetString();
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        bool ordered = el.TryGetProperty("ordered", out var orderedEl) && orderedEl.GetBoolean();
        string? type = el.TryGetProperty("type", out var typeEl) ? typeEl.GetString() : null;
        bool pickable = !el.TryGetProperty("pickable", out var pickableEl) || pickableEl.GetBoolean();
        int? wip = el.TryGetProperty("wip", out var wipEl) && wipEl.ValueKind == JsonValueKind.Number
            ? wipEl.GetInt32() : null;

        return new LaneConfig(name!, ordered, type, pickable, wip);
    }

    private static string? FindLaneDirectory(string root, string laneName)
    {
        var exact = Path.Combine(root, laneName);
        if (Directory.Exists(exact))
        {
            return exact;
        }

        return Directory.Exists(root)
            ? Directory.GetDirectories(root)
                .FirstOrDefault(d => string.Equals(Path.GetFileName(d), laneName, StringComparison.OrdinalIgnoreCase))
            : null;
    }

    private static IEnumerable<WorkItem> LoadOrderedLane(string path, string laneName, bool descendingOrder)
    {
        var folderItems = new List<(WorkItem Item, int Number, string Letter)>();

        foreach (var file in Directory.GetFiles(path, "*.md"))
        {
            var fileName = Path.GetFileName(file);
            var match = Regex.Match(fileName, @"^(\d+)([a-z]?)-(.+)\.md$");
            if (!match.Success)
            {
                continue;
            }

            var number = int.Parse(match.Groups[1].Value);
            var letter = match.Groups[2].Value;

            folderItems.Add((new WorkItem(
                Id: match.Groups[1].Value + letter,
                Slug: match.Groups[3].Value,
                Status: laneName,
                Content: File.ReadAllText(file),
                FileName: fileName,
                FullPath: file
            ), number, letter));
        }

        var ordered = descendingOrder
            ? folderItems.OrderByDescending(x => x.Number).ThenByDescending(x => x.Letter)
            : folderItems.OrderBy(x => x.Number).ThenBy(x => x.Letter);

        return ordered.Select(x => x.Item);
    }

    private static IEnumerable<WorkItem> LoadUnorderedLane(string path, string laneName)
    {
        foreach (var file in Directory.GetFiles(path, "*.md").OrderBy(f => f))
        {
            var fileName = Path.GetFileName(file);
            if (fileName.StartsWith("."))
            {
                continue;
            }

            yield return new WorkItem(
                Id: "",
                Slug: Path.GetFileNameWithoutExtension(fileName),
                Status: laneName,
                Content: File.ReadAllText(file),
                FileName: fileName,
                FullPath: file
            );
        }
    }
}

