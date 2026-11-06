/// <summary>Result of an undo, redo, or history --list operation.</summary>
public sealed record HistoryCommandResult(
    bool IsSuccess,
    string Message,
    HistoryEntry? Entry = null);
