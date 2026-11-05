/// <summary>
/// CLI entry for <c>markban reorder</c> when the order names lettered sub-items. Applies the
/// shared <see cref="SubItemReorderPolicy"/>, then delegates to <see cref="DetachReorderCommand"/>.
/// </summary>
public static class SubItemReorderCommand
{
    public static SubItemReorderOutcome Execute(string rootPath, string folder, string orderArg, SubItemReorderOptions options)
    {
        var ids = orderArg.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries);
        var subIds = ids.Where(SubItemReorderPolicy.IsSubItemId).Distinct().ToList();
        var lane = ResolveLane(rootPath, folder);
        var problem = Validate(rootPath, lane, ids, subIds);
        if (problem is not null)
        {
            return Fail(problem);
        }

        var mode = WorkItemStore.LoadSettings(rootPath).SubItemReorder;
        return ApplyPolicy(rootPath, lane!, ids, subIds, mode, options);
    }

    private static SubItemReorderOutcome ApplyPolicy(
        string rootPath, string lane, string[] ids, List<string> subIds, string mode, SubItemReorderOptions options)
    {
        var decision = SubItemReorderPolicy.Decide(mode);
        if (decision == SubItemReorderDecision.Invalid)
        {
            return Fail(SubItemReorderMode.InvalidValueMessage(mode));
        }

        if (decision == SubItemReorderDecision.Blocked)
        {
            return Fail(SubItemReorderPolicy.CliBlockedMessage);
        }

        PrintPlan(rootPath, subIds);
        if (options.DryRun)
        {
            Console.WriteLine("\nDry run: no files were renamed.");
            return SubItemReorderOutcome.Done;
        }

        var needsPrompt = decision == SubItemReorderDecision.NeedsConfirmation && !options.AssumeYes;
        if (needsPrompt && !Confirm(options))
        {
            return options.InputIsInteractive ? Cancelled() : SubItemReorderOutcome.Failed;
        }

        return Run(rootPath, lane, ids, subIds);
    }

    private static string? Validate(string rootPath, string? lane, string[] ids, List<string> subIds)
    {
        if (lane is null)
        {
            return "Folder is invalid or not an ordered lane.";
        }

        var existing = WorkItemStore.LoadAll(rootPath)
            .Where(i => i.Status == lane).Select(i => i.Id).ToHashSet();
        var missing = ids.FirstOrDefault(id => SubItemReorderPolicy.IsSubItemId(id) && !existing.Contains(id));
        return missing is null ? null : $"sub-item {missing} not found in {lane}.";
    }

    private static string? ResolveLane(string rootPath, string folder)
    {
        var lane = WorkItemStore.LoadConfig(rootPath).FirstOrDefault(l =>
            l.Name.Replace(" ", "").Equals(folder.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
        return lane is { Ordered: true } ? lane.Name : null;
    }

    private static void PrintPlan(string rootPath, List<string> subIds)
    {
        Console.WriteLine("The following sub-items will be detached from their parents and renumbered as standalone items:");
        foreach (var (subItemId, newId) in DetachReorderCommand.PlanDetach(rootPath, subIds))
        {
            Console.WriteLine($"  {subItemId} -> {newId}");
        }

        Console.WriteLine("The lane is then renumbered in the requested order.");
    }

    private static bool Confirm(SubItemReorderOptions options)
    {
        if (!options.InputIsInteractive)
        {
            Console.WriteLine("Not an interactive terminal: treating the confirmation as 'no'. Re-run with --yes to proceed.");
            return false;
        }

        Console.Write("Continue? [y/N] ");
        var answer = options.Input.ReadLine()?.Trim();
        return string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase)
            || string.Equals(answer, "yes", StringComparison.OrdinalIgnoreCase);
    }

    private static SubItemReorderOutcome Run(string rootPath, string lane, string[] ids, List<string> subIds)
    {
        var result = DetachReorderCommand.Execute(rootPath, lane, ids, subIds);
        Console.WriteLine(result.Message);
        return result.IsSuccess ? SubItemReorderOutcome.Done : SubItemReorderOutcome.Failed;
    }

    private static SubItemReorderOutcome Cancelled()
    {
        Console.WriteLine("Cancelled. No changes were made.");
        return SubItemReorderOutcome.Cancelled;
    }

    private static SubItemReorderOutcome Fail(string message)
    {
        Console.Error.WriteLine($"Error: {message}");
        return SubItemReorderOutcome.Failed;
    }
}
