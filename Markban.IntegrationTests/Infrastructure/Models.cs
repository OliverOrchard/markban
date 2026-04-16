using System.Text.Json;

namespace Markban.IntegrationTests;

public record TestWorkItem(string Id, string Slug, string Status, string Content, string FileName, string FullPath);
public record TestWorkItemSummary(string Id, string Slug, string Status);

public static class JsonHelper
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static List<TestWorkItem> DeserializeItems(string json)
        => JsonSerializer.Deserialize<List<TestWorkItem>>(json, Options)!;

    public static List<TestWorkItemSummary> DeserializeSummaries(string json)
        => JsonSerializer.Deserialize<List<TestWorkItemSummary>>(json, Options)!;

    public static TestWorkItem? DeserializeItem(string json)
        => JsonSerializer.Deserialize<TestWorkItem>(json, Options);
}
