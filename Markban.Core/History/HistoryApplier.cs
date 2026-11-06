/// <summary>Verifies file hashes and applies or previews an undo/redo history entry.</summary>
internal static class HistoryApplier
{
    public static HistoryCommandResult Apply(
        string rootPath,
        HistoryEntry entry,
        bool isUndo,
        bool dryRun,
        Action commitJournal)
    {
        var transitions = BuildTransitions(entry, isUndo);
        var modifiedFiles = FindModifiedOrConflictingFiles(rootPath, transitions);
        if (modifiedFiles.Count > 0)
        {
            string actionName = isUndo ? "undo" : "redo";
            string fileList = string.Join(", ", modifiedFiles);
            return new HistoryCommandResult(
                false,
                $"Error: Cannot {actionName} because the following file(s) changed since the operation was recorded: {fileList}");
        }

        if (!dryRun)
        {
            ExecuteFileMoves(rootPath, transitions);
            commitJournal();
        }

        return new HistoryCommandResult(true, FormatSuccessMessage(entry, transitions, isUndo, dryRun), entry);
    }

    private static List<(HistoryFileState Source, HistoryFileState Target)> BuildTransitions(HistoryEntry entry, bool isUndo)
    {
        return entry.Changes
            .Select(c => isUndo ? (c.After, c.Before) : (c.Before, c.After))
            .ToList();
    }

    private static List<string> FindModifiedOrConflictingFiles(
        string rootPath,
        IReadOnlyList<(HistoryFileState Source, HistoryFileState Target)> transitions)
    {
        var sourcePaths = transitions.Select(t => NormalizeRel(t.Source.Path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var problems = new List<string>();
        foreach (var (source, target) in transitions)
        {
            CheckTransitionSafety(rootPath, source, target, sourcePaths, problems);
        }

        return problems.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void CheckTransitionSafety(
        string rootPath,
        HistoryFileState source,
        HistoryFileState target,
        HashSet<string> sourcePaths,
        List<string> problems)
    {
        string sourceFull = ToFullPath(rootPath, source.Path);
        if (!File.Exists(sourceFull) || HistoryContentRestorer.ComputeFileHash(sourceFull) != source.Hash)
        {
            problems.Add(source.Path);
        }

        string targetRel = NormalizeRel(target.Path);
        if (!sourcePaths.Contains(targetRel) && File.Exists(ToFullPath(rootPath, target.Path)))
        {
            problems.Add(target.Path);
        }
    }

    private static void ExecuteFileMoves(
        string rootPath,
        IReadOnlyList<(HistoryFileState Source, HistoryFileState Target)> transitions)
    {
        var staged = new List<(string SourceFull, string TempFull, string TargetFull)>();
        foreach (var (source, target) in transitions)
        {
            staged.Add(StageTransition(rootPath, source, target));
        }

        foreach (var item in staged)
        {
            File.Delete(item.SourceFull);
        }

        foreach (var item in staged)
        {
            File.Move(item.TempFull, item.TargetFull);
        }
    }

    private static (string SourceFull, string TempFull, string TargetFull) StageTransition(
        string rootPath,
        HistoryFileState source,
        HistoryFileState target)
    {
        string sourceFull = ToFullPath(rootPath, source.Path);
        string targetFull = ToFullPath(rootPath, target.Path);
        string targetDir = Path.GetDirectoryName(targetFull)!;
        Directory.CreateDirectory(targetDir);

        var currentBytes = File.ReadAllBytes(sourceFull);
        var restoredBytes = HistoryContentRestorer.RestoreBytes(currentBytes, target);
        string tempFull = Path.Combine(targetDir, $"__tmp_{Guid.NewGuid():N}.md");
        File.WriteAllBytes(tempFull, restoredBytes);
        return (sourceFull, tempFull, targetFull);
    }

    private static string FormatSuccessMessage(
        HistoryEntry entry,
        IReadOnlyList<(HistoryFileState Source, HistoryFileState Target)> transitions,
        bool isUndo,
        bool dryRun)
    {
        string header = (dryRun, isUndo) switch
        {
            (true, true) => $"Dry run: would undo {entry.Operation} ({entry.Description}):",
            (true, false) => $"Dry run: would redo {entry.Operation} ({entry.Description}):",
            (false, true) => $"Undid {entry.Operation} ({entry.Description}):",
            (false, false) => $"Redid {entry.Operation} ({entry.Description}):"
        };

        var lines = transitions.Select(t => $"  {t.Source.Path} -> {t.Target.Path}");
        return string.Join(Environment.NewLine, new[] { header }.Concat(lines));
    }

    private static string ToFullPath(string rootPath, string relativePath)
    {
        string nativeRel = relativePath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetFullPath(Path.Combine(rootPath, nativeRel));
    }

    private static string NormalizeRel(string relativePath) => relativePath.Replace('\\', '/');
}
