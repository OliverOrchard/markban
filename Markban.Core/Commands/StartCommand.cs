/// <summary>
/// Implements <c>markban start &lt;id&gt;</c>.
/// When feature branches are enabled: checks out main, pulls, creates a feature branch, and moves the item.
/// When disabled: moves the item to the next active lane in config order.
/// </summary>
public static class StartCommand
{
    public record StartResult(bool Success, string? BranchName, string? TargetLane, string? ErrorMessage);

    public static async Task<StartResult> ExecuteAsync(
        string rootPath,
        string identifier,
        bool noPull = false,
        bool dryRun = false)
    {
        var settings = WorkItemStore.LoadSettings(rootPath);
        var lanes = WorkItemStore.LoadConfig(rootPath);
        var items = WorkItemStore.LoadAll(rootPath);

        var item = FindWorkItem(items, identifier);
        if (item == null)
        {
            return Fail($"Work item '{identifier}' not found.");
        }

        var nextLane = ResolveNextLane(lanes, item);
        if (nextLane.Error != null)
        {
            return Fail(nextLane.Error);
        }

        var fb = settings.FeatureBranches;
        if (fb?.Enabled != true)
        {
            return ExecuteDisabledStart(rootPath, item, nextLane.Lane!, dryRun);
        }

        return await ExecuteFeatureBranchStartAsync(rootPath, item, nextLane.Lane!, fb, noPull, dryRun);
    }

    private static WorkItem? FindWorkItem(IReadOnlyList<WorkItem> items, string identifier)
    {
        return items.FirstOrDefault(i =>
            i.Id.Equals(identifier, StringComparison.OrdinalIgnoreCase) ||
            i.Slug.Equals(identifier, StringComparison.OrdinalIgnoreCase));
    }

    private static (string? Lane, string? Error) ResolveNextLane(
        IReadOnlyList<LaneConfig> lanes,
        WorkItem item)
    {
        string? nextLane = FindNextActiveLane(lanes, item.Status);
        return nextLane == null
            ? (null, $"No next active lane found after '{item.Status}'.")
            : (nextLane, null);
    }

    private static string? FindNextActiveLane(IReadOnlyList<LaneConfig> lanes, string currentLane)
    {
        var ordered = lanes.Where(l => l.Ordered).ToList();
        int idx = ordered.FindIndex(l => l.Name.Equals(currentLane, StringComparison.OrdinalIgnoreCase));
        if (idx < 0)
        {
            return ordered.FirstOrDefault(l => l.Type != "done" && l.Type != "ready")?.Name;
        }

        for (int i = idx + 1; i < ordered.Count; i++)
        {
            if (ordered[i].Type != "done")
            {
                return ordered[i].Name;
            }
        }

        return null;
    }

    private static StartResult ExecuteDisabledStart(
        string rootPath,
        WorkItem item,
        string nextLane,
        bool dryRun)
    {
        if (dryRun)
        {
            Console.WriteLine($"[dry-run] Would move {item.Id} from '{item.Status}' to '{nextLane}'.");
            return new StartResult(true, null, nextLane, null);
        }

        MoveCommand.Execute(rootPath, item.Id, nextLane);
        return new StartResult(true, null, nextLane, null);
    }

    private static async Task<StartResult> ExecuteFeatureBranchStartAsync(
        string rootPath,
        WorkItem item,
        string nextLane,
        FeatureBranchSettings fb,
        bool noPull,
        bool dryRun)
    {
        string branchName = $"{fb.BranchPrefix}{item.Slug}";
        bool shouldPull = !noPull && fb.PullOnStart;

        if (dryRun)
        {
            PrintFeatureDryRun(item, nextLane, fb.MainBranch, branchName, shouldPull);
            return new StartResult(true, branchName, nextLane, null);
        }

        string repoRoot = FeatureBranchGitRunner.ResolveRepoRoot(rootPath);
        if (await FeatureBranchGitRunner.IsWorkingTreeDirtyAsync(repoRoot))
        {
            return Fail("Working tree has uncommitted changes. Commit or stash before starting.");
        }

        string? startError = await PrepareAndCreateBranchAsync(repoRoot, fb.MainBranch, branchName, shouldPull);
        if (startError != null)
        {
            return Fail(startError);
        }

        return CompleteStartMove(rootPath, repoRoot, item, nextLane, fb.MainBranch, branchName);
    }

    private static void PrintFeatureDryRun(
        WorkItem item,
        string nextLane,
        string mainBranch,
        string branchName,
        bool shouldPull)
    {
        Console.WriteLine($"[dry-run] Would start feature branch for {item.Id}:");
        Console.WriteLine($"  git checkout {mainBranch}");
        if (shouldPull)
        {
            Console.WriteLine("  git pull");
        }

        Console.WriteLine($"  git checkout -b {branchName}");
        Console.WriteLine($"  Move {item.Id} '{item.Status}' -> '{nextLane}'");
    }

    private static async Task<string?> PrepareAndCreateBranchAsync(
        string repoRoot,
        string mainBranch,
        string branchName,
        bool shouldPull)
    {
        Console.WriteLine($"Switching to {mainBranch}...");
        var checkout = await FeatureBranchGitRunner.RunGitAsync(repoRoot, "checkout", mainBranch);
        if (!checkout.Success)
        {
            return $"git checkout {mainBranch} failed: {checkout.ErrorMessage}";
        }

        if (shouldPull)
        {
            Console.WriteLine($"Pulling latest {mainBranch}...");
            var pull = await FeatureBranchGitRunner.RunGitAsync(repoRoot, "pull");
            if (!pull.Success)
            {
                return $"git pull failed: {pull.ErrorMessage}";
            }
        }

        Console.WriteLine($"Creating branch {branchName}...");
        var branch = await FeatureBranchGitRunner.RunGitAsync(repoRoot, "checkout", "-b", branchName);
        return branch.Success ? null : $"git checkout -b {branchName} failed: {branch.ErrorMessage}";
    }

    private static StartResult CompleteStartMove(
        string rootPath,
        string repoRoot,
        WorkItem item,
        string nextLane,
        string mainBranch,
        string branchName)
    {
        MoveCommand.Execute(rootPath, item.Id, nextLane);
        var verify = WorkItemStore.LoadAll(rootPath).FirstOrDefault(i =>
            i.Id.Equals(item.Id, StringComparison.OrdinalIgnoreCase) &&
            i.Slug.Equals(item.Slug, StringComparison.OrdinalIgnoreCase));

        if (verify == null || !verify.Status.Equals(nextLane, StringComparison.OrdinalIgnoreCase))
        {
            RollbackBranchCreation(repoRoot, mainBranch, branchName);
            return Fail($"Failed to move '{item.Id}' to '{nextLane}'; rolled back branch '{branchName}'.");
        }

        Console.WriteLine($"Started work on {item.Id}. Branch: {branchName}");
        return new StartResult(true, branchName, nextLane, null);
    }

    private static void RollbackBranchCreation(string repoRoot, string mainBranch, string branchName)
    {
        FeatureBranchGitRunner.RunGitAsync(repoRoot, "checkout", mainBranch).GetAwaiter().GetResult();
        FeatureBranchGitRunner.RunGitAsync(repoRoot, "branch", "-D", branchName).GetAwaiter().GetResult();
    }

    private static StartResult Fail(string message)
    {
        Console.Error.WriteLine($"Error: {message}");
        return new StartResult(false, null, null, message);
    }
}
