/// <summary>Result of loading a board's history journal from disk.</summary>
public sealed record HistoryLoadResult(HistoryJournal Journal, string? CorruptWarning = null)
{
    public bool IsCorrupt => CorruptWarning is not null;
}
