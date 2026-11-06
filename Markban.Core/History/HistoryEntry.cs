/// <summary>One recorded, undoable board operation.</summary>
public sealed record HistoryEntry(
    string Id,
    string Operation,
    string Description,
    DateTimeOffset Timestamp,
    IReadOnlyList<HistoryFileChange> Changes);
