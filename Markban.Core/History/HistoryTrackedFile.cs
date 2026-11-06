/// <summary>Tracks one work-item file across a recorded operation.</summary>
internal sealed record HistoryTrackedFile(
    HistoryFileState BeforeState,
    byte[] BeforeBytes,
    string CurrentFullPath);
