using System.Text.Json.Nodes;

public static class LaneAddCommand
{
    private static readonly string[] ValidTypes = ["ready", "done"];

    public static LaneAddResult Execute(string rootPath, string name, LaneAddOptions options, bool dryRun = false)
    {
        var config = LoadConfig(rootPath, name, options, out string? configPath, out JsonArray? lanes, out string? error);
        if (config == null)
        {
            return new LaneAddResult(false, error!);
        }

        string laneDir = Path.Combine(rootPath, name);
        string description = Describe(name, options, Directory.Exists(laneDir));
        if (dryRun)
        {
            return new LaneAddResult(true, description, DryRun: true);
        }

        return Apply(config, lanes!, configPath!, laneDir, name, options);
    }

    private static JsonObject? LoadConfig(
        string rootPath, string name, LaneAddOptions options,
        out string? configPath, out JsonArray? lanes, out string? error)
    {
        configPath = WorkItemStore.GetBoardConfigPath(rootPath);
        lanes = null;
        error = LaneConfigFile.ValidateName(name) ?? ValidateType(options.Type);
        if (error != null)
        {
            return null;
        }

        var config = LaneConfigFile.Load(configPath);
        lanes = config?["lanes"] as JsonArray;
        error = lanes == null
            ? $"Error: '{configPath}' must exist and define a 'lanes' array to add a lane."
            : CheckConflicts(rootPath, lanes, name, options.Type);
        return error == null ? config : null;
    }

    private static string? ValidateType(string? type)
        => type == null || ValidTypes.Contains(type, StringComparer.OrdinalIgnoreCase)
            ? null
            : $"Error: Invalid lane type '{type}'. Valid types: ready, done.";

    private static string? CheckConflicts(string rootPath, JsonArray lanes, string name, string? type)
    {
        if (lanes.OfType<JsonObject>().Any(l => LaneConfigFile.NameMatches(l, name)))
        {
            return $"Error: Lane '{name}' already exists in config.";
        }

        if (File.Exists(Path.Combine(rootPath, name)))
        {
            return $"Error: A file named '{name}' already exists; cannot create lane directory.";
        }

        var existing = type == null ? null : LaneConfigFile.FindByType(lanes, type);
        return existing == null
            ? null
            : $"Error: Lane '{existing["name"]}' already has type '{type}'. Only one '{type}' lane is allowed.";
    }

    private static JsonObject BuildEntry(string name, LaneAddOptions options)
    {
        var entry = new JsonObject { ["name"] = name, ["ordered"] = options.Ordered };
        if (options.Type != null)
        {
            entry["type"] = options.Type.ToLowerInvariant();
        }

        if (!options.Pickable)
        {
            entry["pickable"] = false;
        }

        return entry;
    }

    private static string Describe(string name, LaneAddOptions options, bool dirExists)
    {
        string dirLine = dirExists
            ? $"Would reuse existing lane directory: '{name}'"
            : $"Would create lane directory: '{name}'";
        return $"{dirLine}{Environment.NewLine}Would append lane to markban.json: {BuildEntry(name, options).ToJsonString()}";
    }

    private static LaneAddResult Apply(
        JsonObject config, JsonArray lanes, string configPath, string laneDir, string name, LaneAddOptions options)
    {
        bool created = !Directory.Exists(laneDir);
        try
        {
            Directory.CreateDirectory(laneDir);
            lanes.Add(BuildEntry(name, options));
            File.WriteAllText(configPath, LaneConfigFile.Serialize(config));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            TryRemoveCreatedDirectory(created, laneDir);
            return new LaneAddResult(false, $"Error: Failed to add lane '{name}': {ex.Message}");
        }

        return new LaneAddResult(true, $"Added lane '{name}'.");
    }

    private static void TryRemoveCreatedDirectory(bool created, string laneDir)
    {
        try
        {
            if (created && Directory.Exists(laneDir) && !Directory.EnumerateFileSystemEntries(laneDir).Any())
            {
                Directory.Delete(laneDir);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best-effort cleanup; the original error is reported by the caller.
        }
    }
}
