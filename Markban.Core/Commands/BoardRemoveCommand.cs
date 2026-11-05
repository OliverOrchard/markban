using System.Text.Json;
using System.Text.Json.Nodes;

public enum BoardRemoveStatus
{
    Removed,
    DryRun,
    RootBoard,
    NotFound,
    ConfigMissing,
    InvalidConfig,
    WriteFailed,
}

public record BoardRemoveResult(
    BoardRemoveStatus Status,
    string Key,
    string? BoardName = null,
    string? BoardPath = null,
    bool BoardsKeyRemoved = false,
    string? ErrorMessage = null)
{
    public bool IsSuccess => Status is BoardRemoveStatus.Removed or BoardRemoveStatus.DryRun;
}

public static class BoardRemoveCommand
{
    private const string BoardsProperty = "boards";

    /// <summary>
    /// Unregisters a board from the <c>boards</c> array of markban.json in <paramref name="configDir"/>.
    /// The board directory itself is never touched.
    /// </summary>
    public static BoardRemoveResult Execute(string configDir, string rootPath, string key, bool dryRun)
    {
        var configPath = Path.Combine(configDir, "markban.json");
        if (!File.Exists(configPath))
        {
            return new BoardRemoveResult(BoardRemoveStatus.ConfigMissing, key);
        }

        try
        {
            return RemoveFromConfig(configDir, configPath, rootPath, key, dryRun);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            return new BoardRemoveResult(BoardRemoveStatus.InvalidConfig, key, ErrorMessage: ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new BoardRemoveResult(BoardRemoveStatus.WriteFailed, key, ErrorMessage: ex.Message);
        }
    }

    private static BoardRemoveResult RemoveFromConfig(
        string configDir, string configPath, string rootPath, string key, bool dryRun)
    {
        var entry = WorkItemStore.LoadBoards(configDir)
            .FirstOrDefault(b => string.Equals(b.Key, key, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
        {
            return new BoardRemoveResult(BoardRemoveStatus.NotFound, key);
        }

        if (IsRootBoard(entry, configDir, rootPath))
        {
            return new BoardRemoveResult(BoardRemoveStatus.RootBoard, key, entry.Name, entry.ResolvedPath);
        }

        var node = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
        var boardsKeyRemoved = RemoveEntry(node, entry.Key);
        if (dryRun)
        {
            return Result(BoardRemoveStatus.DryRun, entry, boardsKeyRemoved);
        }

        File.WriteAllText(configPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return Result(BoardRemoveStatus.Removed, entry, boardsKeyRemoved);
    }

    private static BoardRemoveResult Result(BoardRemoveStatus status, BoardEntry entry, bool boardsKeyRemoved) =>
        new(status, entry.Key, entry.Name, entry.ResolvedPath, boardsKeyRemoved);

    private static bool IsRootBoard(BoardEntry entry, string configDir, string rootPath) =>
        SamePath(entry.ResolvedPath, rootPath) || SamePath(entry.ResolvedPath, configDir);

    private static bool SamePath(string a, string b) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <summary>Removes the entry from the array in-memory; drops the whole key when it empties. Returns true if the key was dropped.</summary>
    private static bool RemoveEntry(JsonObject config, string key)
    {
        var boards = config[BoardsProperty]!.AsArray();
        var match = boards.First(b => MatchesKey(b, key));
        boards.Remove(match);
        if (boards.Count > 0)
        {
            return false;
        }

        config.Remove(BoardsProperty);
        return true;
    }

    private static bool MatchesKey(JsonNode? board, string key)
    {
        var name = board?["name"]?.GetValue<string>();
        return name != null && string.Equals(SlugHelper.Generate(name), key, StringComparison.OrdinalIgnoreCase);
    }
}
