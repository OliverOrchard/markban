using System.Text.Json;
using System.Text.Json.Nodes;

public static class BoardAddCommand
{
    private const string BoardsProperty = "boards";
    private const string ConfigFileName = "markban.json";
    private const string DefaultWorkItemsRelPath = "./work-items";

    private static readonly HashSet<string> NonLocalConfigKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "rootPath",
        "boards",
        "name",
        "web",
    };

    public static BoardAddResult Execute(
        string configDir,
        string rootPath,
        string name,
        string boardPath,
        bool noLink = false,
        bool copyConfig = false,
        bool dryRun = false) =>
        Execute(configDir, rootPath, new BoardAddOptions(name, boardPath, noLink, copyConfig, dryRun));

    public static BoardAddResult Execute(string configDir, string rootPath, BoardAddOptions options)
    {
        string key = SlugHelper.Generate(options.Name);
        if (string.IsNullOrWhiteSpace(options.Name) || string.IsNullOrWhiteSpace(key))
        {
            return new BoardAddResult(
                BoardAddStatus.InvalidName,
                key,
                options.Name,
                ErrorMessage: $"Board name '{options.Name}' does not produce a usable key.");
        }

        string targetDir = Path.GetFullPath(Path.Combine(configDir, options.BoardPath));
        string configPath = Path.Combine(configDir, ConfigFileName);
        if (RequiresRootConfig(options) && !File.Exists(configPath))
        {
            return new BoardAddResult(BoardAddStatus.ConfigMissing, key, options.Name, targetDir);
        }

        return TryAddBoard(configDir, configPath, rootPath, targetDir, key, options);
    }

    private static bool RequiresRootConfig(BoardAddOptions options) =>
        !options.NoLink || options.CopyConfig;

    private static BoardAddResult TryAddBoard(
        string configDir,
        string configPath,
        string rootPath,
        string targetDir,
        string key,
        BoardAddOptions options)
    {
        try
        {
            return AddBoard(configDir, configPath, rootPath, targetDir, key, options);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            return new BoardAddResult(BoardAddStatus.InvalidConfig, key, options.Name, targetDir, ErrorMessage: ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new BoardAddResult(BoardAddStatus.WriteFailed, key, options.Name, targetDir, ErrorMessage: ex.Message);
        }
    }

    private static BoardAddResult AddBoard(
        string configDir,
        string configPath,
        string rootPath,
        string targetDir,
        string key,
        BoardAddOptions options)
    {
        var conflict = options.NoLink
            ? null
            : CheckLinkConflicts(configDir, configPath, rootPath, targetDir, key, options.Name);
        if (conflict != null)
        {
            return conflict;
        }

        bool isExistingBoard = IsExistingBoard(targetDir);
        JsonObject? newBoardConfig = isExistingBoard ? null : BuildNewBoardConfig(configPath, options);
        JsonObject? rootConfig = options.NoLink ? null : ParseJsonObject(configPath);
        bool rootBoardAdded = rootConfig != null && WillAddRootBoard(rootConfig, configDir, rootPath);

        if (rootConfig != null)
        {
            AppendBoardEntry(rootConfig, configDir, rootPath, options, targetDir);
        }

        if (!options.DryRun)
        {
            ApplyChanges(targetDir, newBoardConfig, configPath, rootConfig);
        }

        BoardAddStatus status = options.DryRun ? BoardAddStatus.DryRun : BoardAddStatus.Added;
        return new BoardAddResult(
            status,
            key,
            options.Name,
            targetDir,
            Bootstrapped: !isExistingBoard,
            Linked: !options.NoLink,
            RootBoardAdded: rootBoardAdded);
    }

    private static BoardAddResult? CheckLinkConflicts(
        string configDir,
        string configPath,
        string rootPath,
        string targetDir,
        string key,
        string boardName)
    {
        if (HasDuplicateKey(configDir, configPath, rootPath, key))
        {
            return new BoardAddResult(BoardAddStatus.DuplicateKey, key, boardName, targetDir);
        }

        string? existingKey = FindDuplicatePathKey(configDir, configPath, rootPath, targetDir);
        if (existingKey == null)
        {
            return null;
        }

        return new BoardAddResult(
            BoardAddStatus.DuplicatePath,
            key,
            boardName,
            targetDir,
            ErrorMessage: $"Board path '{targetDir}' is already registered under key '{existingKey}'.",
            ExistingKey: existingKey);
    }

    private static void ApplyChanges(
        string targetDir,
        JsonObject? newBoardConfig,
        string configPath,
        JsonObject? rootConfig)
    {
        if (newBoardConfig != null)
        {
            BootstrapBoardDirectory(targetDir, newBoardConfig);
        }

        if (rootConfig != null)
        {
            WriteJson(configPath, rootConfig);
        }
    }

    private static void BootstrapBoardDirectory(string targetDir, JsonObject newBoardConfig)
    {
        Directory.CreateDirectory(targetDir);
        string targetConfigPath = Path.Combine(targetDir, ConfigFileName);
        WriteJson(targetConfigPath, newBoardConfig);
        string targetWorkItemsDir = Path.Combine(targetDir, "work-items");
        WorkItemStore.EnsureLaneDirectories(targetWorkItemsDir);
    }

    private static bool IsExistingBoard(string targetDir)
    {
        if (!Directory.Exists(targetDir))
        {
            return false;
        }

        string targetConfigPath = Path.Combine(targetDir, ConfigFileName);
        if (File.Exists(targetConfigPath))
        {
            using var _ = JsonDocument.Parse(File.ReadAllText(targetConfigPath));
            return true;
        }

        return ResolvesToExistingBoardRoot(targetDir);
    }

    private static bool ResolvesToExistingBoardRoot(string targetDir)
    {
        try
        {
            WorkItemStore.ResolveConfiguredBoardRoot(targetDir);
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    private static bool HasDuplicateKey(string configDir, string configPath, string rootPath, string key)
    {
        var existingBoards = WorkItemStore.LoadBoards(configDir);
        if (existingBoards.Any(b => string.Equals(b.Key, key, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var rootConfig = ParseJsonObject(configPath);
        string rootBoardName = ResolveRootBoardName(rootConfig, configDir, rootPath);
        string rootBoardKey = SlugHelper.Generate(rootBoardName);
        return string.Equals(rootBoardKey, key, StringComparison.OrdinalIgnoreCase);
    }

    private static string? FindDuplicatePathKey(
        string configDir,
        string configPath,
        string rootPath,
        string targetDir)
    {
        var existingMatch = WorkItemStore.LoadBoards(configDir)
            .FirstOrDefault(b => SamePath(b.ResolvedPath, targetDir));
        if (existingMatch != null)
        {
            return existingMatch.Key;
        }

        var rootConfig = ParseJsonObject(configPath);
        return MatchesRootBoardPath(rootConfig, configDir, rootPath, targetDir)
            ? SlugHelper.Generate(ResolveRootBoardName(rootConfig, configDir, rootPath))
            : null;
    }

    private static bool MatchesRootBoardPath(
        JsonObject rootConfig,
        string configDir,
        string rootPath,
        string targetDir)
    {
        string rootBoardPath = GetRootBoardConfiguredPath(rootConfig, configDir, rootPath);
        string resolvedRoot = Path.GetFullPath(Path.Combine(configDir, rootBoardPath));
        return SamePath(targetDir, resolvedRoot) || SamePath(targetDir, configDir);
    }

    private static bool WillAddRootBoard(JsonObject rootConfig, string configDir, string rootPath)
    {
        if (rootConfig[BoardsProperty] is not JsonArray boards)
        {
            return true;
        }

        string rootBoardPath = GetRootBoardConfiguredPath(rootConfig, configDir, rootPath);
        string resolvedRoot = Path.GetFullPath(Path.Combine(configDir, rootBoardPath));
        return !boards.Any(b => IsSameBoardPath(b, configDir, resolvedRoot));
    }

    private static void AppendBoardEntry(
        JsonObject rootConfig,
        string configDir,
        string rootPath,
        BoardAddOptions options,
        string targetDir)
    {
        if (rootConfig[BoardsProperty] is not JsonArray)
        {
            rootConfig[BoardsProperty] = new JsonArray();
        }

        var boards = rootConfig[BoardsProperty]!.AsArray();
        EnsureRootBoardPresent(boards, rootConfig, configDir, rootPath);
        string entryPath = FormatPortablePath(configDir, options.BoardPath, targetDir);
        boards.Add(CreateBoardJson(options.Name, entryPath));
    }

    private static void EnsureRootBoardPresent(
        JsonArray boards,
        JsonObject rootConfig,
        string configDir,
        string rootPath)
    {
        string rootBoardPath = GetRootBoardConfiguredPath(rootConfig, configDir, rootPath);
        string resolvedRoot = Path.GetFullPath(Path.Combine(configDir, rootBoardPath));
        if (boards.Any(b => IsSameBoardPath(b, configDir, resolvedRoot)))
        {
            return;
        }

        string rootBoardName = ResolveRootBoardName(rootConfig, configDir, rootPath);
        boards.Insert(0, CreateBoardJson(rootBoardName, rootBoardPath));
    }

    private static bool IsSameBoardPath(JsonNode? boardNode, string configDir, string targetFullPath)
    {
        string? configuredPath = boardNode?["path"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            return false;
        }

        string resolved = Path.GetFullPath(Path.Combine(configDir, configuredPath));
        return SamePath(resolved, targetFullPath);
    }

    private static bool SamePath(string a, string b) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static string ResolveRootBoardName(JsonObject rootConfig, string configDir, string rootPath)
    {
        string? configuredName = rootConfig["name"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(configuredName))
        {
            return configuredName.Trim();
        }

        string rootBoardPath = GetRootBoardConfiguredPath(rootConfig, configDir, rootPath);
        string resolvedRoot = Path.GetFullPath(Path.Combine(configDir, rootBoardPath));
        string dirName = Path.GetFileName(Path.TrimEndingDirectorySeparator(resolvedRoot));
        return char.ToUpperInvariant(dirName[0]) + dirName[1..];
    }

    private static string GetRootBoardConfiguredPath(JsonObject rootConfig, string configDir, string rootPath)
    {
        string? configured = rootConfig["rootPath"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        string relative = Path.GetRelativePath(configDir, rootPath).Replace('\\', '/');
        return relative.StartsWith('.') ? relative : "./" + relative;
    }

    private static string FormatPortablePath(string configDir, string rawPath, string targetDir)
    {
        if (!Path.IsPathRooted(rawPath))
        {
            return rawPath.Replace('\\', '/');
        }

        return Path.GetRelativePath(configDir, targetDir).Replace('\\', '/');
    }

    private static JsonObject CreateBoardJson(string name, string path) =>
        new()
        {
            ["name"] = name,
            ["path"] = path,
        };

    private static JsonObject BuildNewBoardConfig(string rootConfigPath, BoardAddOptions options)
    {
        var config = BuildDefaultBoardConfig(options.Name);
        if (!options.CopyConfig)
        {
            return config;
        }

        var sourceConfig = ParseJsonObject(rootConfigPath);
        CopyBoardLocalSettings(sourceConfig, config);
        return config;
    }

    private static void CopyBoardLocalSettings(JsonObject source, JsonObject target)
    {
        foreach (var property in source)
        {
            if (NonLocalConfigKeys.Contains(property.Key) || property.Value == null)
            {
                continue;
            }

            CopyProperty(target, property.Key, property.Value);
        }
    }

    private static void CopyProperty(JsonObject target, string key, JsonNode value)
    {
        if (target[key] is JsonObject targetObj && value is JsonObject sourceObj)
        {
            foreach (var child in sourceObj)
            {
                targetObj[child.Key] = child.Value?.DeepClone();
            }

            return;
        }

        target[key] = value.DeepClone();
    }

    private static JsonObject BuildDefaultBoardConfig(string name)
    {
        string defaultJson = $$"""
            {
              "rootPath": "{{DefaultWorkItemsRelPath}}",
              "name": {{JsonSerializer.Serialize(name)}},
              "lanes": [
                { "name": "Todo", "ordered": true, "type": "ready" },
                { "name": "In Progress", "ordered": true },
                { "name": "Testing", "ordered": true },
                { "name": "Done", "ordered": true, "type": "done" },
                { "name": "Ideas", "ordered": false, "pickable": false },
                { "name": "Rejected", "ordered": false, "pickable": false }
              ],
              "heading": {
                "enabled": true
              },
              "slugs": {
                "casing": "kebab"
              },
              "commit": {
                "maxMessageLength": 72,
                "tags": ["feat", "fix", "docs", "style", "refactor", "test", "build", "ci", "chore", "revert", "perf"]
              },
              "git": {
                "enabled": true,
                "featureBranches": {
                  "enabled": false,
                  "mainBranch": "main",
                  "branchPrefix": "feature/",
                  "commitStrategy": "single",
                  "pullOnStart": true,
                  "checkoutOnDone": true,
                  "prCommand": ""
                }
              },
              "web": {
                "port": 5000
              },
              "blocked": {
                "enabled": true
              },
              "tags": {
                "enabled": true
              },
              "dependsOn": {
                "enabled": true
              },
              "subItemReorder": "warn",
              "customFrontmatter": []
            }
            """;
        return JsonNode.Parse(defaultJson)!.AsObject();
    }

    private static JsonObject ParseJsonObject(string configPath)
    {
        var node = JsonNode.Parse(File.ReadAllText(configPath));
        return node as JsonObject ?? throw new InvalidDataException($"Invalid markban.json at '{configPath}': expected a JSON object.");
    }

    private static void WriteJson(string path, JsonObject node) =>
        File.WriteAllText(path, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
}
