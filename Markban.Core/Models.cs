public record WorkItem(string Id, string Slug, string Status, string Content, string FileName, string FullPath);
public record WorkItemSummary(string Id, string Slug, string Status);
public record HelpEntry(string Usage, string Description, string? Detail = null);
public record LaneConfig(string Name, bool Ordered, string? Type = null, bool Pickable = true, int? Wip = null);
public record BoardSettings(
    int CommitMaxMessageLength = 72,
    IReadOnlyList<string>? CommitTags = null,
    bool HeadingEnabled = true,
    string SlugCasing = "kebab");

public static class BoardConfig
{
    public static readonly IReadOnlyList<LaneConfig> DefaultLanes =
    [
        new LaneConfig("Todo", true, "ready"),
        new LaneConfig("In Progress", true),
        new LaneConfig("Testing", true),
        new LaneConfig("Done", true, "done"),
        new LaneConfig("Ideas", false, Pickable: false),
        new LaneConfig("Rejected", false, Pickable: false),
    ];
}
