/// <summary>One file's before/after state within a recorded operation.</summary>
public sealed record HistoryFileChange(HistoryFileState Before, HistoryFileState After);
