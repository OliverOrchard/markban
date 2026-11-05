using System.Text.RegularExpressions;

/// <summary>
/// Shared by CLI and web: detaches the named sub-items into standalone items, then reorders the lane.
/// Sub-item ids in the list that are not detached stay attached to their parent and are skipped.
/// </summary>
public static class DetachReorderCommand
{
    private const string ErrorPrefix = "Error:";

    public static DetachSubItemResult Execute(string rootPath, string lane, IEnumerable<string> orderedIds, IReadOnlyCollection<string> detachIds)
    {
        var ids = orderedIds.ToList();
        foreach (var detachId in detachIds)
        {
            var detach = DetachSubItemCommand.Execute(rootPath, lane, detachId);
            if (!detach.IsSuccess)
            {
                return detach;
            }

            ids = ids.Select(id => id == detachId ? detach.NewId! : id).ToList();
        }

        return Reorder(rootPath, lane, ids);
    }

    /// <summary>Lists the standalone ids the given sub-items would receive, in order.</summary>
    public static IReadOnlyList<(string SubItemId, string NewId)> PlanDetach(string rootPath, IReadOnlyList<string> detachIds)
    {
        var next = HighestPrimaryNumber(rootPath) + 1;
        return detachIds.Select((id, i) => (id, (next + i).ToString())).ToList();
    }

    private static int HighestPrimaryNumber(string rootPath)
    {
        return WorkItemStore.LoadAll(rootPath)
            .Select(i => Regex.Match(i.Id, @"^\d+"))
            .Where(m => m.Success)
            .Select(m => int.Parse(m.Value))
            .DefaultIfEmpty(0)
            .Max();
    }

    private static DetachSubItemResult Reorder(string rootPath, string lane, IEnumerable<string> ids)
    {
        var orderArg = string.Join(",", ids.Where(id => id.Length > 0 && id.All(char.IsAsciiDigit)));
        var output = CaptureOutput(() => ReorderCommand.Execute(rootPath, lane, orderArg, noSubItems: false, dryRun: false));
        var isError = output.StartsWith(ErrorPrefix, StringComparison.Ordinal) || output.Contains($"\n{ErrorPrefix}");
        return new DetachSubItemResult(!isError, output);
    }

    private static string CaptureOutput(Action action)
    {
        var originalOut = Console.Out;
        var buffer = new StringWriter();
        Console.SetOut(buffer);
        try
        {
            action();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        return buffer.ToString().Trim();
    }
}
