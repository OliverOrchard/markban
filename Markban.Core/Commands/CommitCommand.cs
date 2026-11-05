using System.Text.RegularExpressions;

public static class CommitCommand
{
    public static readonly string[] ValidTags =
    [
        "feat", "fix", "docs", "style", "refactor", "test", "build", "ci", "chore", "revert", "perf"
    ];

    private static readonly HashSet<string> ValidStrategies = new(StringComparer.OrdinalIgnoreCase)
    {
        "single", "multiple", "squash"
    };

    public record CommitResult(bool Success, string? ErrorMessage);

    public static IReadOnlyList<string> GetValidTags(string rootPath)
    {
        var configured = WorkItemStore.LoadSettings(rootPath).CommitTags;
        return configured != null && configured.Count > 0 ? configured : ValidTags;
    }

    public static Task<CommitResult> ExecuteAsync(
        string rootPath,
        string identifier,
        string tag,
        string message,
        bool dryRun = false)
        => ExecuteAsync(rootPath, [identifier], tag, message, dryRun);

    public static async Task<CommitResult> ExecuteAsync(
        string rootPath,
        IReadOnlyList<string> identifiers,
        string tag,
        string message,
        bool dryRun = false)
    {
        var settings = WorkItemStore.LoadSettings(rootPath);
        string? inputError = ValidateInputs(rootPath, settings, ref tag, ref message);
        if (inputError != null)
        {
            return Fail(inputError);
        }

        var doneLane = ResolveDoneLane(WorkItemStore.LoadConfig(rootPath));
        var resolved = doneLane != null ? ResolveItems(identifiers, WorkItemStore.LoadAll(rootPath)) : null;
        if (doneLane == null || resolved == null)
        {
            return new CommitResult(false, "Failed to resolve target lane or work items.");
        }

        string repoRoot = FeatureBranchGitRunner.ResolveRepoRoot(rootPath);
        string commitMessage = $"{tag}: {message}";

        if (dryRun)
        {
            await ExecuteDryRunAsync(repoRoot, settings.FeatureBranches, resolved, doneLane.Name, commitMessage);
            return new CommitResult(true, null);
        }

        return settings.FeatureBranches?.Enabled == true
            ? await ExecuteFeatureCommitAsync(rootPath, repoRoot, settings.FeatureBranches, resolved, doneLane.Name, commitMessage)
            : await ExecuteStandardCommitAsync(rootPath, repoRoot, resolved, doneLane.Name, commitMessage);
    }

    private static string? ValidateInputs(
        string rootPath,
        BoardSettings settings,
        ref string tag,
        ref string message)
    {
        var validTags = GetValidTags(rootPath);
        if (!validTags.Contains(tag, StringComparer.OrdinalIgnoreCase))
        {
            return $"Invalid tag '{tag}'. Valid tags: {string.Join(", ", validTags)}";
        }

        tag = tag.ToLowerInvariant();
        message = message.Trim();

        string? msgError = ValidateMessage(message, settings.CommitMaxMessageLength);
        if (msgError != null)
        {
            return msgError;
        }

        var fb = settings.FeatureBranches;
        if (fb?.Enabled == true && !ValidStrategies.Contains(fb.CommitStrategy))
        {
            return $"Invalid commitStrategy '{fb.CommitStrategy}'. Valid strategies: single, multiple, squash.";
        }

        return null;
    }

    private static async Task ExecuteDryRunAsync(
        string repoRoot,
        FeatureBranchSettings? fb,
        IReadOnlyList<WorkItem> items,
        string doneLane,
        string commitMessage)
    {
        PrintDryRunHeader(items, doneLane);
        var status = await FeatureBranchGitRunner.RunGitAsync(repoRoot, "status", "--short");
        if (!string.IsNullOrWhiteSpace(status.StdOut))
        {
            Console.WriteLine(status.StdOut);
        }

        Console.WriteLine();
        Console.WriteLine("Would run:");
        PrintDryRunGitSteps(fb, commitMessage, await FeatureBranchGitRunner.HasUpstreamBranchAsync(repoRoot));
        Console.WriteLine();
        Console.WriteLine("=== End dry run. No changes made. ===");
    }

    private static void PrintDryRunGitSteps(FeatureBranchSettings? fb, string commitMessage, bool hasUpstream)
    {
        string strategy = fb?.Enabled == true ? fb.CommitStrategy.ToLowerInvariant() : "single";
        Console.WriteLine("  git add .");
        if (strategy == "squash" && fb != null)
        {
            Console.WriteLine($"  git reset --soft {fb.MainBranch}");
        }

        Console.WriteLine($"  git commit -m \"{commitMessage}\"");
        Console.WriteLine(DescribeDryRunPush(fb, strategy, hasUpstream));

        if (fb?.Enabled == true)
        {
            if (!string.IsNullOrWhiteSpace(fb.PrCommand))
            {
                Console.WriteLine($"  {fb.PrCommand}   (prCommand)");
            }

            if (fb.CheckoutOnDone)
            {
                Console.WriteLine($"  git checkout {fb.MainBranch} && git pull");
            }
        }
    }

    private static string DescribeDryRunPush(FeatureBranchSettings? fb, string strategy, bool hasUpstream)
    {
        if (fb?.Enabled != true || !hasUpstream)
        {
            return fb?.Enabled == true ? "  git push -u origin <branch>" : "  git push";
        }

        return strategy == "squash" ? "  git push --force-with-lease" : "  git push";
    }

    private static async Task<CommitResult> ExecuteStandardCommitAsync(
        string rootPath,
        string repoRoot,
        IReadOnlyList<WorkItem> items,
        string doneLaneName,
        string commitMessage)
    {
        MoveItemsToDone(rootPath, items, doneLaneName);

        string? commitError = await StageAndCommitAsync(repoRoot, commitMessage);
        if (commitError != null)
        {
            return Fail(commitError);
        }

        string? pushError = await PushCurrentBranchAsync(repoRoot, useUpstreamFallback: false);
        if (pushError != null)
        {
            return Fail(pushError);
        }

        Console.WriteLine();
        Console.WriteLine($"Done. Committed and pushed: {commitMessage}");
        return new CommitResult(true, null);
    }

    private static async Task<CommitResult> ExecuteFeatureCommitAsync(
        string rootPath,
        string repoRoot,
        FeatureBranchSettings fb,
        IReadOnlyList<WorkItem> items,
        string doneLaneName,
        string commitMessage)
    {
        int priorAheadCount = await FeatureBranchGitRunner.GetAheadCommitCountAsync(repoRoot, fb.MainBranch);
        WarnIfSingleStrategyHasPriorCommits(fb, priorAheadCount);

        MoveItemsToDone(rootPath, items, doneLaneName);

        string? strategyError = await ApplyCommitStrategyAsync(repoRoot, fb, commitMessage, priorAheadCount);
        if (strategyError != null)
        {
            return Fail(strategyError);
        }

        string? pushError = await PushCurrentBranchAsync(repoRoot, useUpstreamFallback: true, forceWithLease: IsSquash(fb));
        if (pushError != null)
        {
            return Fail(pushError);
        }

        Console.WriteLine();
        Console.WriteLine($"Done. Committed and pushed: {commitMessage}");
        return await CompleteFeaturePostPushAsync(repoRoot, fb);
    }

    private static void WarnIfSingleStrategyHasPriorCommits(FeatureBranchSettings fb, int priorAheadCount)
    {
        if (fb.CommitStrategy.Equals("single", StringComparison.OrdinalIgnoreCase) && priorAheadCount > 0)
        {
            Console.WriteLine(
                $"Warning: Branch already has {priorAheadCount} commit(s) ahead of '{fb.MainBranch}' under 'single' commit strategy.");
        }
    }

    private static async Task<string?> ApplyCommitStrategyAsync(
        string repoRoot,
        FeatureBranchSettings fb,
        string commitMessage,
        int priorAheadCount)
    {
        string strategy = fb.CommitStrategy.ToLowerInvariant();
        if (strategy == "multiple")
        {
            return await ApplyMultipleStrategyAsync(repoRoot, fb.MainBranch, commitMessage, priorAheadCount);
        }

        if (strategy == "squash")
        {
            return await ApplySquashStrategyAsync(repoRoot, fb.MainBranch, commitMessage);
        }

        return await StageAndCommitAsync(repoRoot, commitMessage);
    }

    private static async Task<string?> ApplyMultipleStrategyAsync(
        string repoRoot,
        string mainBranch,
        string commitMessage,
        int priorAheadCount)
    {
        bool isDirty = await FeatureBranchGitRunner.IsWorkingTreeDirtyAsync(repoRoot);
        if (!isDirty)
        {
            return priorAheadCount > 0
                ? null
                : $"No commits ahead of '{mainBranch}' and no changes to commit.";
        }

        return await StageAndCommitAsync(repoRoot, commitMessage);
    }

    private static async Task<string?> ApplySquashStrategyAsync(
        string repoRoot,
        string mainBranch,
        string commitMessage)
    {
        Console.WriteLine();
        Console.WriteLine("git add .");
        var add = await FeatureBranchGitRunner.RunGitAsync(repoRoot, "add", ".");
        if (!add.Success)
        {
            return $"git add failed: {add.ErrorMessage}";
        }

        Console.WriteLine($"git reset --soft {mainBranch}");
        var reset = await FeatureBranchGitRunner.RunGitAsync(repoRoot, "reset", "--soft", mainBranch);
        if (!reset.Success)
        {
            return $"git reset --soft {mainBranch} failed: {reset.ErrorMessage}";
        }

        Console.WriteLine($"git commit -m \"{commitMessage}\"");
        var commit = await FeatureBranchGitRunner.RunGitAsync(repoRoot, "commit", "-m", commitMessage);
        return commit.Success ? null : $"git commit failed: {commit.ErrorMessage}";
    }

    private static async Task<string?> StageAndCommitAsync(string repoRoot, string commitMessage)
    {
        Console.WriteLine();
        Console.WriteLine("git add .");
        var add = await FeatureBranchGitRunner.RunGitAsync(repoRoot, "add", ".");
        if (!add.Success)
        {
            return $"git add failed: {add.ErrorMessage}";
        }

        Console.WriteLine();
        Console.WriteLine($"git commit -m \"{commitMessage}\"");
        var commit = await FeatureBranchGitRunner.RunGitAsync(repoRoot, "commit", "-m", commitMessage);
        return commit.Success ? null : $"git commit failed: {commit.ErrorMessage}";
    }

    private static bool IsSquash(FeatureBranchSettings fb)
    {
        return fb.CommitStrategy.Equals("squash", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string?> PushCurrentBranchAsync(
        string repoRoot,
        bool useUpstreamFallback,
        bool forceWithLease = false)
    {
        Console.WriteLine();
        bool hasUpstream = !useUpstreamFallback || await FeatureBranchGitRunner.HasUpstreamBranchAsync(repoRoot);
        if (hasUpstream)
        {
            string[] pushArgs = forceWithLease ? ["push", "--force-with-lease"] : ["push"];
            Console.WriteLine($"git {string.Join(' ', pushArgs)}");
            var push = await FeatureBranchGitRunner.RunGitAsync(repoRoot, pushArgs);
            return push.Success ? null : $"git push failed: {push.ErrorMessage}";
        }

        string branch = await FeatureBranchGitRunner.GetCurrentBranchAsync(repoRoot) ?? "HEAD";
        Console.WriteLine($"git push -u origin {branch}");
        var upstreamPush = await FeatureBranchGitRunner.RunGitAsync(repoRoot, "push", "-u", "origin", branch);
        return upstreamPush.Success ? null : $"git push failed: {upstreamPush.ErrorMessage}";
    }

    private static async Task<CommitResult> CompleteFeaturePostPushAsync(
        string repoRoot,
        FeatureBranchSettings fb)
    {
        string? prError = await RunPrCommandIfConfiguredAsync(repoRoot, fb.PrCommand);
        if (prError != null)
        {
            return Fail(prError);
        }

        if (fb.CheckoutOnDone)
        {
            string? checkoutError = await ReturnToMainBranchAsync(repoRoot, fb.MainBranch);
            if (checkoutError != null)
            {
                return Fail(checkoutError);
            }
        }

        return new CommitResult(true, null);
    }

    private static async Task<string?> RunPrCommandIfConfiguredAsync(string repoRoot, string? prCommand)
    {
        if (string.IsNullOrWhiteSpace(prCommand))
        {
            Console.WriteLine();
            Console.WriteLine("No prCommand configured. Open a pull request manually from your branch.");
            return null;
        }

        Console.WriteLine();
        Console.WriteLine($"Opening PR: {prCommand}");
        var prResult = await FeatureBranchGitRunner.RunShellCommandAsync(repoRoot, prCommand);
        if (!string.IsNullOrWhiteSpace(prResult.StdOut))
        {
            Console.WriteLine(prResult.StdOut);
        }

        if (!string.IsNullOrWhiteSpace(prResult.StdErr))
        {
            Console.Error.WriteLine(prResult.StdErr);
        }

        return prResult.Success
            ? null
            : "PR command failed. Branch pushed; open the PR manually.";
    }

    private static async Task<string?> ReturnToMainBranchAsync(string repoRoot, string mainBranch)
    {
        Console.WriteLine();
        Console.WriteLine($"Returning to {mainBranch}...");
        var checkout = await FeatureBranchGitRunner.RunGitAsync(repoRoot, "checkout", mainBranch);
        if (!checkout.Success)
        {
            return $"git checkout {mainBranch} failed: {checkout.ErrorMessage}";
        }

        var pull = await FeatureBranchGitRunner.RunGitAsync(repoRoot, "pull");
        return pull.Success ? null : $"git pull failed: {pull.ErrorMessage}";
    }

    private static void MoveItemsToDone(string rootPath, IReadOnlyList<WorkItem> items, string doneLaneName)
    {
        foreach (var item in items)
        {
            if (item.Status.Equals(doneLaneName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            Console.WriteLine($"Moving '{item.Id}' ({item.Slug}) to {doneLaneName}...");
            MoveCommand.Execute(rootPath, string.IsNullOrEmpty(item.Id) ? item.Slug : item.Id, doneLaneName, overrideWip: true);
        }
    }

    private static LaneConfig? ResolveDoneLane(IReadOnlyList<LaneConfig> lanes)
    {
        var doneLanes = lanes.Where(l => l.Type == "done").ToList();
        if (doneLanes.Count == 0)
        {
            Console.Error.WriteLine("Error: No lane with type 'done' configured. Add \"type\": \"done\" to a lane in markban.json.");
            return null;
        }

        if (doneLanes.Count > 1)
        {
            Console.Error.WriteLine($"Error: Multiple lanes have type 'done' ({string.Join(", ", doneLanes.Select(l => l.Name))}). Only one is allowed.");
            return null;
        }

        return doneLanes[0];
    }

    private static IReadOnlyList<WorkItem>? ResolveItems(IReadOnlyList<string> identifiers, IReadOnlyList<WorkItem> items)
    {
        var resolved = new List<WorkItem>();
        foreach (var id in identifiers)
        {
            var item = items.FirstOrDefault(i =>
                i.Id.Equals(id, StringComparison.OrdinalIgnoreCase) ||
                i.Slug.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (item == null)
            {
                Console.Error.WriteLine($"Error: Work item '{id}' not found.");
                return null;
            }

            resolved.Add(item);
        }

        return resolved;
    }

    private static void PrintDryRunHeader(IReadOnlyList<WorkItem> items, string doneLane)
    {
        Console.WriteLine("=== DRY RUN — nothing will be changed ===");
        Console.WriteLine();
        foreach (var item in items)
        {
            Console.WriteLine($"Would move '{item.Id}' ({item.Slug}) from {item.Status} -> {doneLane}");
        }

        Console.WriteLine();
        Console.WriteLine("Current git status:");
    }

    internal static string? ValidateMessage(string message, int maxLen = 72)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Message cannot be empty.";
        }

        if (message.Length > maxLen)
        {
            return $"Message is too long ({message.Length} chars, max {maxLen}). Keep it to one short sentence.";
        }

        if (Regex.IsMatch(message, @"[.!?]\s+\S"))
        {
            return "Message looks like more than one sentence. Keep commits to a single, concise thought.";
        }

        int andCount = Regex.Matches(message, @"\band\b", RegexOptions.IgnoreCase).Count;
        if (andCount >= 3)
        {
            return "Message looks like a run-on list. Pick the one key thing this commit does.";
        }

        return null;
    }

    private static CommitResult Fail(string message)
    {
        Console.Error.WriteLine($"Error: {message}");
        return new CommitResult(false, message);
    }
}
