using System.Text.Json;
using System.Text.RegularExpressions;

public static class WorkItemStore
{
    private const string ConfigFileName = "markban.json";

    public static string FindRoot(string? startDir = null)
    {
        var current = startDir ?? Directory.GetCurrentDirectory();
        while (current != null)
        {
            var configuredRoot = TryGetConfiguredRootPath(current, throwOnInvalidJson: false);
            if (configuredRoot != null)
            {
                return configuredRoot;
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

        var configPath = GetConfigPath(projectDir);
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

        var configPath = GetConfigPath(projectDir);
        if (!File.Exists(configPath))
        {
            return new BoardSettings();
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            var maxLen = 72;
            IReadOnlyList<string>? commitTags = null;
            var headingEnabled = true;
            var slugCasing = "kebab";
            var blockedEnabled = true;
            var tagsEnabled = true;
            var dependsOnEnabled = true;
            IReadOnlyList<CustomFrontmatterField>? customFrontmatter = null;

            if (doc.RootElement.TryGetProperty("commit", out var commitEl))
            {
                if (commitEl.TryGetProperty("maxMessageLength", out var maxLenEl))
                {
                    maxLen = maxLenEl.GetInt32();
                }

                if (commitEl.TryGetProperty("tags", out var tagsEl))
                {
                    commitTags = tagsEl.EnumerateArray()
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

            if (doc.RootElement.TryGetProperty("blocked", out var blockedEl)
                && blockedEl.TryGetProperty("enabled", out var blockedEnabledEl))
            {
                blockedEnabled = blockedEnabledEl.GetBoolean();
            }

            if (doc.RootElement.TryGetProperty("tags", out var tagsFeatureEl)
                && tagsFeatureEl.TryGetProperty("enabled", out var tagsEnabledEl))
            {
                tagsEnabled = tagsEnabledEl.GetBoolean();
            }

            if (doc.RootElement.TryGetProperty("dependsOn", out var depsEl)
                && depsEl.TryGetProperty("enabled", out var depsEnabledEl))
            {
                dependsOnEnabled = depsEnabledEl.GetBoolean();
            }

            if (doc.RootElement.TryGetProperty("customFrontmatter", out var customEl)
                && customEl.ValueKind == JsonValueKind.Array)
            {
                customFrontmatter = customEl.EnumerateArray()
                    .Select(ParseCustomField)
                    .OfType<CustomFrontmatterField>()
                    .ToList();
            }

            FeatureBranchSettings? featureBranches = null;
            if (doc.RootElement.TryGetProperty("git", out var gitEl)
                && gitEl.TryGetProperty("featureBranches", out var fbEl)
                && fbEl.ValueKind == JsonValueKind.Object)
            {
                featureBranches = ParseFeatureBranchSettings(fbEl);
            }

            return new BoardSettings(maxLen, commitTags, headingEnabled, slugCasing,
                blockedEnabled, tagsEnabled, dependsOnEnabled, customFrontmatter, featureBranches);
        }
        catch (JsonException)
        {
            return new BoardSettings();
        }
    }

    private static CustomFrontmatterField? ParseCustomField(JsonElement el)
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!el.TryGetProperty("name", out var nameEl) || nameEl.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var name = nameEl.GetString();
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        string? defaultValue = null;
        var hasDefault = false;
        if (el.TryGetProperty("default", out var defaultEl))
        {
            hasDefault = true;
            defaultValue = defaultEl.ValueKind switch
            {
                JsonValueKind.String => defaultEl.GetString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => defaultEl.ToString(),
                _ => null  // JsonValueKind.Null → hasDefault=true, Default=null
            };
        }

        return new CustomFrontmatterField(name!, defaultValue, hasDefault);
    }

    private static FeatureBranchSettings ParseFeatureBranchSettings(JsonElement el)
    {
        var enabled = el.TryGetProperty("enabled", out var enabledEl) && enabledEl.GetBoolean();
        var mainBranch = el.TryGetProperty("mainBranch", out var mbEl) ? mbEl.GetString() ?? "main" : "main";
        var strategy = el.TryGetProperty("commitStrategy", out var csEl) ? csEl.GetString() ?? "single" : "single";
        var pullOnStart = !el.TryGetProperty("pullOnStart", out var posEl) || posEl.GetBoolean();
        var checkoutOnDone = !el.TryGetProperty("checkoutOnDone", out var codEl) || codEl.GetBoolean();
        var prCmd = el.TryGetProperty("prCommand", out var prEl) ? prEl.GetString() : null;
        var prefix = el.TryGetProperty("branchPrefix", out var bpEl) ? bpEl.GetString() ?? "feature/" : "feature/";
        return new FeatureBranchSettings(enabled, mainBranch, strategy, pullOnStart, checkoutOnDone, prCmd, prefix);
    }

    public static IReadOnlyList<BoardEntry> LoadBoards(string configDir)
    {
        var configPath = GetConfigPath(configDir);
        if (!File.Exists(configPath))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            if (!doc.RootElement.TryGetProperty("boards", out var boardsEl))
            {
                return [];
            }

            if (boardsEl.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException("Invalid 'boards' config: expected an array.");
            }

            var boards = boardsEl.EnumerateArray()
                .Select((element, index) => ParseBoardEntry(element, configDir, index))
                .ToList();

            EnsureUniqueBoardKeys(boards);
            return boards;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Invalid markban.json at '{configPath}': {ex.Message}", ex);
        }
    }

    public static string ResolveConfiguredBoardRoot(string boardPath)
    {
        if (!Directory.Exists(boardPath))
        {
            throw new DirectoryNotFoundException($"Board path not found: {boardPath}");
        }

        var configuredRoot = TryGetConfiguredRootPath(boardPath, throwOnInvalidJson: true);
        if (configuredRoot != null)
        {
            return EnsureBoardRootExists(configuredRoot, boardPath);
        }

        var nestedBoardRoot = Path.Combine(boardPath, "work-items");
        if (Directory.Exists(nestedBoardRoot))
        {
            return nestedBoardRoot;
        }

        if (LooksLikeBoardRoot(boardPath))
        {
            return boardPath;
        }

        throw new DirectoryNotFoundException(
            $"Board path '{boardPath}' does not contain a work-items directory or board lanes.");
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

    private static BoardEntry ParseBoardEntry(JsonElement element, string configDir, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"Invalid boards[{index}] entry: expected an object.");
        }

        var name = GetRequiredBoardValue(element, "name", index);
        var configuredPath = GetRequiredBoardValue(element, "path", index);
        var key = SlugHelper.Generate(name);
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidDataException($"Invalid boards[{index}] entry: name '{name}' does not produce a usable key.");
        }

        return new BoardEntry(name, key, Path.GetFullPath(Path.Combine(configDir, configuredPath)));
    }

    private static string GetRequiredBoardValue(JsonElement element, string propertyName, int index)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"Invalid boards[{index}] entry: '{propertyName}' must be a non-empty string.");
        }

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"Invalid boards[{index}] entry: '{propertyName}' must be a non-empty string.");
        }

        return value;
    }

    private static void EnsureUniqueBoardKeys(IReadOnlyList<BoardEntry> boards)
    {
        var duplicateKey = boards
            .GroupBy(board => board.Key, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateKey != null)
        {
            throw new InvalidDataException(
                $"Duplicate board key '{duplicateKey.Key}' in 'boards'. Rename one of the boards to make its key unique.");
        }
    }

    private static string? TryGetConfiguredRootPath(string configDir, bool throwOnInvalidJson)
    {
        var configPath = GetConfigPath(configDir);
        if (!File.Exists(configPath))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            if (!doc.RootElement.TryGetProperty("rootPath", out var rootPathElement))
            {
                return null;
            }

            var relativePath = rootPathElement.GetString();
            return string.IsNullOrWhiteSpace(relativePath)
                ? null
                : Path.GetFullPath(Path.Combine(configDir, relativePath));
        }
        catch (JsonException) when (!throwOnInvalidJson)
        {
            return null;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Invalid markban.json at '{configPath}': {ex.Message}", ex);
        }
    }

    private static string EnsureBoardRootExists(string boardRoot, string boardPath)
    {
        if (!Directory.Exists(boardRoot))
        {
            throw new DirectoryNotFoundException(
                $"Board '{boardPath}' resolves to a missing root path: {boardRoot}");
        }

        return boardRoot;
    }

    private static bool LooksLikeBoardRoot(string boardPath)
    {
        var laneDirectories = Directory.GetDirectories(boardPath)
            .Select(Path.GetFileName)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return BoardConfig.DefaultLanes.Any(lane => laneDirectories.Contains(lane.Name));
    }

    private static string GetConfigPath(string configDir) => Path.Combine(configDir, ConfigFileName);

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

            var content = File.ReadAllText(file);
            folderItems.Add((new WorkItem(
                Id: match.Groups[1].Value + letter,
                Slug: match.Groups[3].Value,
                Status: laneName,
                Content: content,
                FileName: fileName,
                FullPath: file,
                Blocked: FrontmatterParser.GetField(content, "blocked"),
                Tags: FrontmatterParser.GetListField(content, "tags"),
                DependsOn: FrontmatterParser.GetListField(content, "dependsOn")
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
            if (fileName.StartsWith('.'))
            {
                continue;
            }

            var content = File.ReadAllText(file);
            yield return new WorkItem(
                Id: "",
                Slug: Path.GetFileNameWithoutExtension(fileName),
                Status: laneName,
                Content: content,
                FileName: fileName,
                FullPath: file,
                Blocked: FrontmatterParser.GetField(content, "blocked"),
                Tags: FrontmatterParser.GetListField(content, "tags"),
                DependsOn: FrontmatterParser.GetListField(content, "dependsOn")
            );
        }
    }
}
