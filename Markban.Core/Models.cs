public record WorkItem(string Id, string Slug, string Status, string Content, string FileName, string FullPath);
public record WorkItemSummary(string Id, string Slug, string Status);
public record HelpEntry(string Usage, string Description, string? Detail = null);
