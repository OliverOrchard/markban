using System.Text;

/// <summary>Formats the undo and redo stacks for <c>markban undo --list</c> and <c>markban redo --list</c>.</summary>
public static class HistoryListCommand
{
    public static HistoryCommandResult Execute(string rootPath)
    {
        lock (HistoryGate.Sync)
        {
            var settings = HistorySettings.Load(rootPath);
            if (!settings.Enabled)
            {
                return new HistoryCommandResult(true, "History is disabled in markban.json (history.enabled is false).");
            }

            var load = HistoryStore.Load(rootPath);
            if (load.IsCorrupt)
            {
                return new HistoryCommandResult(false, $"Error: {load.CorruptWarning}");
            }

            return new HistoryCommandResult(true, FormatStacks(load.Journal));
        }
    }

    private static string FormatStacks(HistoryJournal journal)
    {
        var sb = new StringBuilder();
        AppendStack(sb, "Undo stack", journal.Undo);
        sb.AppendLine();
        AppendStack(sb, "Redo stack", journal.Redo);
        return sb.ToString().TrimEnd();
    }

    private static void AppendStack(StringBuilder sb, string title, IReadOnlyList<HistoryEntry> entries)
    {
        sb.AppendLine($"{title} ({entries.Count}):");
        if (entries.Count == 0)
        {
            sb.AppendLine("  (empty)");
            return;
        }

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            int rank = entries.Count - i;
            sb.AppendLine($"  [{rank}] {FormatEntry(entries[i])}");
        }
    }

    private static string FormatEntry(HistoryEntry entry)
    {
        string timestamp = entry.Timestamp.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss'Z'");
        string files = string.Join(", ", entry.Changes.Select(c => $"{c.Before.Path} -> {c.After.Path}"));
        return $"{timestamp}  {entry.Operation}  {entry.Description}  ({files})";
    }
}
