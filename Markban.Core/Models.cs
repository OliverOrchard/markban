public record WorkItem(
    string Id,
    string Slug,
    string Status,
    string Content,
    string FileName,
    string FullPath,
    string? Blocked = null,
    IReadOnlyList<string>? Tags = null,
    IReadOnlyList<string>? DependsOn = null);
public record WorkItemSummary(string Id, string Slug, string Status);
public record HelpEntry(string Usage, string Description, string? Detail = null);
public record LaneConfig(string Name, bool Ordered, string? Type = null, bool Pickable = true, int? Wip = null);

/// <summary>A configured board entry resolved from the <c>boards</c> array in markban.json.</summary>
/// <param name="Name">Display name from config.</param>
/// <param name="Key">URL-safe, stable identifier derived from the name.</param>
/// <param name="ResolvedPath">Absolute path from the top-level config to the configured board location.</param>
public record BoardEntry(string Name, string Key, string ResolvedPath);

public record BoardSettings(
    int CommitMaxMessageLength = 72,
    IReadOnlyList<string>? CommitTags = null,
    bool HeadingEnabled = true,
    string SlugCasing = "kebab",
    bool BlockedEnabled = true,
    bool TagsEnabled = true,
    bool DependsOnEnabled = true,
    IReadOnlyList<CustomFrontmatterField>? CustomFrontmatter = null,
    FeatureBranchSettings? FeatureBranches = null);

public record CustomFrontmatterField(string Name, string? Default, bool HasDefault = false);

public record FeatureBranchSettings(
    bool Enabled = false,
    string MainBranch = "main",
    string CommitStrategy = "single",
    bool PullOnStart = true,
    bool CheckoutOnDone = true,
    string? PrCommand = null,
    string BranchPrefix = "feature/");

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
