using System.Text.Json;

/// <summary>The <c>history</c> section of markban.json.</summary>
public sealed record HistorySettings(bool Enabled = true, int MaxEntries = HistorySettings.DefaultMaxEntries)
{
    public const int DefaultMaxEntries = 50;

    /// <summary>Reads the history settings, falling back to the defaults for anything missing or invalid.</summary>
    public static HistorySettings Load(string root)
    {
        var configPath = WorkItemStore.GetBoardConfigPath(root);
        if (!File.Exists(configPath))
        {
            return new HistorySettings();
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            return FromRoot(doc.RootElement);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new HistorySettings();
        }
    }

    internal static HistorySettings FromRoot(JsonElement rootElement)
    {
        if (rootElement.ValueKind != JsonValueKind.Object
            || !rootElement.TryGetProperty("history", out var history)
            || history.ValueKind != JsonValueKind.Object)
        {
            return new HistorySettings();
        }

        return new HistorySettings(ReadEnabled(history), ReadMaxEntries(history));
    }

    private static bool ReadEnabled(JsonElement history)
    {
        bool found = history.TryGetProperty("enabled", out var enabled);
        return !found || enabled.ValueKind is not (JsonValueKind.True or JsonValueKind.False) || enabled.GetBoolean();
    }

    private static int ReadMaxEntries(JsonElement history)
    {
        bool found = history.TryGetProperty("maxEntries", out var max);
        bool valid = found && max.ValueKind == JsonValueKind.Number && max.TryGetInt32(out int value) && value >= 1;
        return valid ? max.GetInt32() : DefaultMaxEntries;
    }
}
