using System.Text;
using System.Text.Json;

/// <summary>Reads and writes the per-board undo/redo history journal (<c>.markban/history.json</c>).</summary>
public static class HistoryStore
{
    private const string DirectoryName = ".markban";
    private const string DefaultFileName = "history.json";

    private static readonly UTF8Encoding Utf8NoBom = new(false);
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string GetJournalPath(string rootPath)
    {
        string fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
        string? parentDir = Path.GetDirectoryName(fullRoot);
        if (parentDir is null || IsSystemTempRoot(parentDir))
        {
            return Path.Combine(fullRoot, DirectoryName, DefaultFileName);
        }

        string fileName = ResolveJournalFileName(parentDir, fullRoot);
        return Path.Combine(parentDir, DirectoryName, fileName);
    }

    public static HistoryLoadResult Load(string rootPath)
    {
        string path = GetJournalPath(rootPath);
        if (!File.Exists(path))
        {
            return new HistoryLoadResult(HistoryJournal.Empty);
        }

        try
        {
            return ParseJournal(path, File.ReadAllText(path, Encoding.UTF8));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return CorruptResult(path);
        }
    }

    public static void Save(string rootPath, HistoryJournal journal)
    {
        string path = GetJournalPath(rootPath);
        string dir = Path.GetDirectoryName(path)!;
        EnsureSelfIgnoringDirectory(dir);
        string json = JsonSerializer.Serialize(journal, SerializerOptions);
        File.WriteAllText(path, json, Utf8NoBom);
    }

    private static void EnsureSelfIgnoringDirectory(string directoryPath)
    {
        Directory.CreateDirectory(directoryPath);
        string gitignorePath = Path.Combine(directoryPath, ".gitignore");
        if (!File.Exists(gitignorePath))
        {
            File.WriteAllText(gitignorePath, "*\n", Utf8NoBom);
        }
    }

    private static HistoryLoadResult ParseJournal(string path, string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!HasValidRootArrays(doc.RootElement))
        {
            return CorruptResult(path);
        }

        var journal = JsonSerializer.Deserialize<HistoryJournal>(json, SerializerOptions);
        return journal is not null && AreEntriesValid(journal)
            ? new HistoryLoadResult(journal)
            : CorruptResult(path);
    }

    private static bool HasValidRootArrays(JsonElement root)
    {
        return root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("undo", out var undo)
            && undo.ValueKind == JsonValueKind.Array
            && root.TryGetProperty("redo", out var redo)
            && redo.ValueKind == JsonValueKind.Array;
    }

    private static bool AreEntriesValid(HistoryJournal journal)
    {
        return journal.Undo is not null
            && journal.Redo is not null
            && journal.Undo.All(IsEntryValid)
            && journal.Redo.All(IsEntryValid);
    }

    private static bool IsEntryValid(HistoryEntry? entry)
    {
        return entry is not null
            && !string.IsNullOrWhiteSpace(entry.Operation)
            && entry.Changes is { Count: > 0 }
            && entry.Changes.All(IsChangeValid);
    }

    private static bool IsChangeValid(HistoryFileChange? change)
    {
        return change?.Before is not null
            && change.After is not null
            && !string.IsNullOrWhiteSpace(change.Before.Path)
            && !string.IsNullOrWhiteSpace(change.Before.Hash)
            && !string.IsNullOrWhiteSpace(change.After.Path)
            && !string.IsNullOrWhiteSpace(change.After.Hash);
    }

    private static HistoryLoadResult CorruptResult(string path) =>
        new(HistoryJournal.Empty, $"History journal at '{path}' is corrupt or unreadable.");

    private static bool IsSystemTempRoot(string dir)
    {
        string tempRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        string candidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(dir));
        return string.Equals(tempRoot, candidate, StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveJournalFileName(string parentDir, string fullRoot)
    {
        try
        {
            var boards = WorkItemStore.LoadBoards(parentDir);
            if (boards.Count <= 1 || IsSamePath(boards[0].ResolvedPath, fullRoot))
            {
                return DefaultFileName;
            }

            string slug = SlugHelper.Generate(Path.GetFileName(fullRoot));
            return string.IsNullOrWhiteSpace(slug) ? DefaultFileName : $"history-{slug}.json";
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException)
        {
            return DefaultFileName;
        }
    }

    private static bool IsSamePath(string left, string right)
    {
        string a = Path.TrimEndingDirectorySeparator(Path.GetFullPath(left));
        string b = Path.TrimEndingDirectorySeparator(Path.GetFullPath(right));
        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
