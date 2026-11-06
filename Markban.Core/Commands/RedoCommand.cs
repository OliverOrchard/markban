/// <summary>Re-applies the most recently undone history entry and moves it back to the undo stack.</summary>
public static class RedoCommand
{
    public const string DisabledMessage = "Error: History is disabled in markban.json (history.enabled is false).";
    public const string EmptyStackMessage = "Nothing to redo: the redo history stack is empty.";

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

            if (load.Journal.Redo.Count == 0)
            {
                return new HistoryCommandResult(false, EmptyStackMessage);
            }

            var entry = load.Journal.Redo[^1];
            return HistoryApplier.Apply(
                rootPath,
                entry,
                isUndo: false,
                dryRun,
                () => MoveTopRedoToUndo(rootPath, load.Journal, settings.MaxEntries));
        }
    }

    private static void MoveTopRedoToUndo(string rootPath, HistoryJournal journal, int maxEntries)
    {
        var entry = journal.Redo[^1];
        var remainingRedo = journal.Redo.Take(journal.Redo.Count - 1).ToList();
        var updatedUndo = journal.Undo.Append(entry).TakeLast(maxEntries).ToList();
        HistoryStore.Save(rootPath, new HistoryJournal(HistoryJournal.CurrentVersion, updatedUndo, remainingRedo));
    }
}
