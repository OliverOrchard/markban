/// <summary>
/// The persisted undo/redo stacks of a board. Both stacks are stored oldest first, so the
/// most recent entry is the last element.
/// </summary>
public sealed record HistoryJournal(int Version, IReadOnlyList<HistoryEntry> Undo, IReadOnlyList<HistoryEntry> Redo)
{
    public const int CurrentVersion = 1;

    public static HistoryJournal Empty => new(CurrentVersion, [], []);
}
