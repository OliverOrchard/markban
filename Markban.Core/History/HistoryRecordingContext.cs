using System.Text;

/// <summary>Collects before/after states for files touched during a single recorded operation.</summary>
internal sealed class HistoryRecordingContext
{
    private readonly List<HistoryTrackedFile> _files = [];

    public HistoryRecordingContext(string rootPath, string operation, string description)
    {
        RootPath = Path.GetFullPath(rootPath);
        Operation = operation;
        Description = description;
    }

    public string RootPath { get; }

    public string Operation { get; }

    public string Description { get; }

    public void CaptureBefore(string fullPath)
    {
        string normalized = Path.GetFullPath(fullPath);
        if (FindIndex(normalized) >= 0 || !File.Exists(normalized))
        {
            return;
        }

        var bytes = File.ReadAllBytes(normalized);
        var state = CreateState(RootPath, normalized, bytes);
        _files.Add(new HistoryTrackedFile(state, bytes, normalized));
    }

    public void TrackMove(string oldFullPath, string newFullPath)
    {
        int index = FindIndex(Path.GetFullPath(oldFullPath));
        if (index < 0)
        {
            return;
        }

        _files[index] = _files[index] with { CurrentFullPath = Path.GetFullPath(newFullPath) };
    }

    public IReadOnlyList<HistoryFileChange> BuildChanges()
    {
        var changes = new List<HistoryFileChange>();
        foreach (var tracked in _files)
        {
            var change = TryBuildChange(tracked);
            if (change is not null)
            {
                changes.Add(change);
            }
        }

        return changes;
    }

    private HistoryFileChange? TryBuildChange(HistoryTrackedFile tracked)
    {
        if (!File.Exists(tracked.CurrentFullPath))
        {
            return null;
        }

        var afterBytes = File.ReadAllBytes(tracked.CurrentFullPath);
        var after = CreateState(RootPath, tracked.CurrentFullPath, afterBytes);
        var before = tracked.BeforeState;
        if (before.Path == after.Path && before.Hash == after.Hash)
        {
            return null;
        }

        return new HistoryFileChange(
            AttachFallbackContent(before, tracked.BeforeBytes, afterBytes),
            AttachFallbackContent(after, afterBytes, tracked.BeforeBytes));
    }

    private static HistoryFileState AttachFallbackContent(HistoryFileState target, byte[] targetBytes, byte[] oppositeBytes)
    {
        if (HistoryContentRestorer.CanRestoreWithoutContent(oppositeBytes, target))
        {
            return target;
        }

        return target with { Content = Encoding.UTF8.GetString(targetBytes) };
    }

    private static HistoryFileState CreateState(string rootPath, string fullPath, byte[] bytes)
    {
        string relativePath = Path.GetRelativePath(rootPath, fullPath).Replace('\\', '/');
        string? number = HistoryContentRestorer.ExtractNumber(Path.GetFileName(fullPath));
        string hash = HistoryContentRestorer.ComputeHash(bytes);
        return new HistoryFileState(relativePath, number, hash);
    }

    private int FindIndex(string normalizedPath)
    {
        return _files.FindIndex(f =>
            string.Equals(f.CurrentFullPath, normalizedPath, StringComparison.OrdinalIgnoreCase));
    }
}
