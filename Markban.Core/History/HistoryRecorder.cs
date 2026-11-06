/// <summary>Records successful non-dry-run reorder and move operations into the board history journal.</summary>
public static class HistoryRecorder
{
    [ThreadStatic]
    private static HistoryRecordingContext? _current;

    [ThreadStatic]
    private static int _suppressionDepth;

    /// <summary>Suppresses history recording on the current thread until the returned scope is disposed.</summary>
    public static IDisposable Suppress()
    {
        _suppressionDepth++;
        return new HistorySuppressionScope(() => _suppressionDepth--);
    }

    public static void Record(string rootPath, string operation, string description, bool dryRun, Action action)
    {
        Record(rootPath, operation, description, dryRun, () =>
        {
            action();
            return true;
        });
    }

    public static T Record<T>(string rootPath, string operation, string description, bool dryRun, Func<T> action)
    {
        if (!ShouldStartRecording(rootPath, dryRun))
        {
            return action();
        }

        var context = new HistoryRecordingContext(rootPath, operation, description);
        _current = context;
        try
        {
            var result = action();
            CommitContext(context);
            return result;
        }
        finally
        {
            _current = null;
        }
    }

    public static void CaptureBefore(string fullPath) => _current?.CaptureBefore(fullPath);

    public static void TrackMove(string oldFullPath, string newFullPath) => _current?.TrackMove(oldFullPath, newFullPath);

    private static bool ShouldStartRecording(string rootPath, bool dryRun)
    {
        return !dryRun
            && _current is null
            && _suppressionDepth == 0
            && HistorySettings.Load(rootPath).Enabled;
    }

    private static void CommitContext(HistoryRecordingContext context)
    {
        var changes = context.BuildChanges();
        if (changes.Count == 0)
        {
            return;
        }

        var entry = new HistoryEntry(
            Guid.NewGuid().ToString("N")[..12],
            context.Operation,
            context.Description,
            DateTimeOffset.UtcNow,
            changes);
        AppendEntry(context.RootPath, entry);
    }

    private static void AppendEntry(string rootPath, HistoryEntry entry)
    {
        lock (HistoryGate.Sync)
        {
            var settings = HistorySettings.Load(rootPath);
            if (!settings.Enabled)
            {
                return;
            }

            var load = HistoryStore.Load(rootPath);
            ReportCorruptWarning(load);
            var undo = load.Journal.Undo.Append(entry).TakeLast(settings.MaxEntries).ToList();
            TrySave(rootPath, new HistoryJournal(HistoryJournal.CurrentVersion, undo, []));
        }
    }

    private static void ReportCorruptWarning(HistoryLoadResult load)
    {
        if (load.IsCorrupt)
        {
            Console.Error.WriteLine($"Warning: {load.CorruptWarning} Resetting history journal.");
        }
    }

    private static void TrySave(string rootPath, HistoryJournal journal)
    {
        try
        {
            HistoryStore.Save(rootPath, journal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Warning: Could not write history journal: {ex.Message}");
        }
    }
}
