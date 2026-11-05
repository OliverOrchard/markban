using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

public static class LaneRenameCommand
{
    private static readonly JsonSerializerOptions IndentedJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static LaneRenameResult Execute(string rootPath, string oldName, string newName, bool dryRun = false)
        => ExecuteCore(rootPath, oldName, newName, dryRun, Directory.Move, File.WriteAllText);

    internal static LaneRenameResult ExecuteCore(
        string rootPath,
        string oldName,
        string newName,
        bool dryRun,
        Action<string, string> moveDirectory,
        Action<string, string> writeConfig)
    {
        var plan = CreatePlan(rootPath, oldName, newName, out string? error);
        if (plan == null)
        {
            return Fail(error!);
        }

        if (dryRun)
        {
            return new LaneRenameResult(true, DescribeDryRun(plan), DryRun: true);
        }

        return Apply(plan, moveDirectory, writeConfig);
    }

    private static LaneRenamePlan? CreatePlan(string rootPath, string oldName, string newName, out string? error)
    {
        error = ValidateName(oldName) ?? ValidateName(newName);
        if (error != null)
        {
            return null;
        }

        string configPath = WorkItemStore.GetBoardConfigPath(rootPath);
        var config = LoadConfig(configPath);
        if (config?["lanes"] is not JsonArray lanes)
        {
            error = $"Error: '{configPath}' must exist and define a 'lanes' array to rename a lane.";
            return null;
        }

        var lane = lanes.OfType<JsonObject>().FirstOrDefault(l => NameMatches(l, oldName));
        error = lane == null ? $"Error: Lane '{oldName}' is not a configured lane." : CheckTarget(rootPath, lanes, lane, newName);
        return error != null ? null : BuildPlan(rootPath, configPath, config, lane!, newName);
    }

    private static string? ValidateName(string name)
    {
        bool invalid = string.IsNullOrWhiteSpace(name)
            || name.Trim() != name
            || name is "." or ".."
            || name.IndexOfAny([.. Path.GetInvalidFileNameChars(), '/', '\\']) >= 0;
        return invalid ? $"Error: '{name}' is not a valid lane name. Use a single directory name." : null;
    }

    private static JsonObject? LoadConfig(string configPath)
    {
        if (!File.Exists(configPath))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(File.ReadAllText(configPath)) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool NameMatches(JsonObject lane, string name)
        => string.Equals(lane["name"]?.GetValue<string>(), name, StringComparison.OrdinalIgnoreCase);

    private static string? CheckTarget(string rootPath, JsonArray lanes, JsonObject lane, string newName)
    {
        string current = lane["name"]!.GetValue<string>();
        if (current == newName)
        {
            return $"Error: Lane '{current}' is already named '{newName}'.";
        }

        bool nameTaken = lanes.OfType<JsonObject>().Any(l => l != lane && NameMatches(l, newName));
        if (nameTaken || (!IsCaseOnlyChange(current, newName) && PathExists(Path.Combine(rootPath, newName))))
        {
            return $"Error: Lane or directory '{newName}' already exists.";
        }

        return Directory.Exists(Path.Combine(rootPath, current))
            ? null
            : $"Error: Lane directory '{current}' does not exist under '{rootPath}'.";
    }

    private static bool PathExists(string path) => Directory.Exists(path) || File.Exists(path);

    private static bool IsCaseOnlyChange(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static LaneRenamePlan BuildPlan(string rootPath, string configPath, JsonObject config, JsonObject lane, string newName)
    {
        string original = File.ReadAllText(configPath);
        string oldName = lane["name"]!.GetValue<string>();
        lane["name"] = newName;
        string updated = config.ToJsonString(IndentedJson) + Environment.NewLine;
        return new LaneRenamePlan(oldName, newName, Path.Combine(rootPath, oldName), Path.Combine(rootPath, newName), configPath, original, updated);
    }

    private static string DescribeDryRun(LaneRenamePlan plan)
        => $"Would rename lane directory: '{plan.OldName}' -> '{plan.NewName}'{Environment.NewLine}" +
           $"Would update markban.json lane name: '{plan.OldName}' -> '{plan.NewName}'";

    private static LaneRenameResult Apply(LaneRenamePlan plan, Action<string, string> move, Action<string, string> write)
    {
        try
        {
            MoveDirectory(move, plan.SourcePath, plan.TargetPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Fail($"Error: Failed to rename lane directory '{plan.OldName}': {ex.Message}");
        }

        try
        {
            write(plan.ConfigPath, plan.UpdatedConfig);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Rollback(plan, move, ex);
        }

        return new LaneRenameResult(true, $"Renamed lane '{plan.OldName}' to '{plan.NewName}'.");
    }

    private static LaneRenameResult Rollback(LaneRenamePlan plan, Action<string, string> move, Exception cause)
    {
        string prefix = $"Error: Failed to update '{plan.ConfigPath}' ({cause.Message}).";
        try
        {
            File.WriteAllText(plan.ConfigPath, plan.OriginalConfig);
            MoveDirectory(move, plan.TargetPath, plan.SourcePath);
            return Fail($"{prefix} Rolled back directory rename to '{plan.OldName}'.", rolledBack: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Fail($"{prefix} Rollback failed ({ex.Message}).");
        }
    }

    private static void MoveDirectory(Action<string, string> move, string source, string target)
    {
        if (!IsCaseOnlyChange(source, target))
        {
            move(source, target);
            return;
        }

        string temp = $"{source}.{Guid.NewGuid():N}.tmp";
        move(source, temp);
        move(temp, target);
    }

    private static LaneRenameResult Fail(string message, bool rolledBack = false)
        => new(false, message, RolledBack: rolledBack);
}
