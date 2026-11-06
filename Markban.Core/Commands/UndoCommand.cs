/// <summary>Reverses the most recent history entry and moves it to the redo stack.</summary>
public static class UndoCommand
{
    public const string DisabledMessage = "Error: History is disabled in markban.json (history.enabled is false).";
    public const string EmptyStackMessage = "Nothing to undo: the undo history stack is empty.";

    public static HistoryCommandResult Execute(string rootPath, bool dryRun = false)
    {
        lock (HistoryGate.Sync)
        {
            var settings = HistorySettings.Load(rootPath);
            if (!settings.Enabled)
            {
                return new HistoryCommandResult(false, DisabledMessage);
            }

            var load = HistoryStore.Load(rootPath);
            if (load.IsCorrupt)
            {
                return new HistoryCommandResult(false, $"Error: {load.CorruptWarning}");
            }

            if (load.Journal.Undo.Count == 0)
            {
                return new HistoryCommandResult(false, EmptyStackMessage);
            }

            var entry = load.Journal.Undo[^1];
            return HistoryApplier.Apply(
                rootPath,
                entry,
                isUndo: true,
                dryRun,
                () => MoveTopUndoToRedo(rootPath, load.Journal, settings.MaxEntries));
        }
    }

    private static void MoveTopUndoToRedo(string rootPath, HistoryJournal journal, int maxEntries)
    {
        var entry = journal.Undo[^1];
        var remainingUndo = journal.Undo.Take(journal.Undo.Count - 1).ToList();
        var updatedRedo = journal.Redo.Append(entry).TakeLast(maxEntries).ToList();
        HistoryStore.Save(rootPath, new HistoryJournal(HistoryJournal.CurrentVersion, remainingUndo, updatedRedo));
    }
}
