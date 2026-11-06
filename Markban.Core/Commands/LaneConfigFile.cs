using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>Reusable helpers for reading, validating and serializing the <c>lanes</c> array in markban.json.</summary>
internal static class LaneConfigFile
{
    private static readonly JsonSerializerOptions IndentedJson = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    internal static string? ValidateName(string name)
    {
        bool invalid = string.IsNullOrWhiteSpace(name)
            || name.Trim() != name
            || name is "." or ".."
            || name.IndexOfAny([.. Path.GetInvalidFileNameChars(), '/', '\\']) >= 0;
        return invalid ? $"Error: '{name}' is not a valid lane name. Use a single directory name." : null;
    }

    internal static JsonObject? Load(string configPath)
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

    internal static bool NameMatches(JsonObject lane, string name)
        => string.Equals(lane["name"]?.GetValue<string>(), name, StringComparison.OrdinalIgnoreCase);

    internal static JsonObject? FindByType(JsonArray lanes, string type)
        => lanes.OfType<JsonObject>().FirstOrDefault(
            l => string.Equals(l["type"]?.GetValue<string>(), type, StringComparison.OrdinalIgnoreCase));

    internal static string Serialize(JsonObject config)
        => config.ToJsonString(IndentedJson) + Environment.NewLine;
}
