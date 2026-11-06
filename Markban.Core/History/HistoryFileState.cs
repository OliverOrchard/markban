using System.Text.Json.Serialization;

/// <summary>The state of one work-item file on one side of a recorded change.</summary>
/// <param name="Path">Path relative to the board root, using forward slashes (e.g. <c>Todo/3-slug.md</c>).</param>
/// <param name="Number">The item number with optional sub-item letter (e.g. <c>3</c> or <c>3a</c>); null in unordered lanes.</param>
/// <param name="Hash">Lowercase hex SHA-256 of the file content.</param>
/// <param name="Content">Optional content fallback when body sanitization altered more than the heading.</param>
public sealed record HistoryFileState(
    string Path,
    string? Number,
    string Hash,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Content = null);
