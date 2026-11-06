namespace Markban;

/// <summary>Payload returned by <c>GET /api/history</c>.</summary>
public sealed record WebHistoryResponse(
    bool Enabled,
    int MaxEntries,
    bool CanUndo,
    bool CanRedo,
    IReadOnlyList<HistoryEntry> Undo,
    IReadOnlyList<HistoryEntry> Redo);
