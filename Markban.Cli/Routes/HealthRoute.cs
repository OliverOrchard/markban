public class HealthRoute : CommandRoute
{
    public override string? SubCommand => "health";

    public override HelpEntry Help => new HelpEntry(
        "health [check-links|check-order] [--include-ideas] [--fix]",
        "Run board diagnostics (no subcommand runs all checks)",
        "  check-links                  check for broken [slug] cross-references\n" +
        "  check-links --include-ideas  also scan Ideas and Rejected lanes\n" +
        "  check-order                  check numeric ordering\n" +
        "  check-order --fix            auto-fix ordering where safe\n" +
        "  (no subcommand)              run all checks");

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "health")
        {
            return false;
        }

        var subcommand = args.Length > 1 ? args[1] : null;

        switch (subcommand)
        {
            case "check-links":
                RunCheckLinks(rootPath, args);
                break;
            case "check-order":
                RunCheckOrder(rootPath, args);
                break;
            case null:
                RunAll(rootPath);
                break;
            default:
                Console.Error.WriteLine($"Unknown health subcommand '{subcommand}'. Valid: check-links, check-order");
                break;
        }

        return true;
    }

    private static void RunCheckLinks(string rootPath, string[] args)
    {
        var includeIdeas = args.Contains("--include-ideas");
        var items = WorkItemStore.LoadAll(rootPath);
        var (broken, numericRefs) = CheckLinksCommand.Execute(rootPath, items, includeIdeas);
        var settings = WorkItemStore.LoadSettings(rootPath);
        var brokenDeps = settings.DependsOnEnabled
            ? CheckLinksCommand.ValidateDependsOn(items)
            : null;

        CheckLinksCommand.PrintResults(broken, numericRefs, brokenDeps);
        if (broken.Count > 0 || numericRefs.Count > 0 || (brokenDeps != null && brokenDeps.Count > 0))
        {
            Environment.ExitCode = 1;
        }
    }

    private static void RunCheckOrder(string rootPath, string[] args)
    {
        var fix = args.Contains("--fix");
        var (hasIssues, messages) = CheckOrderCommand.Execute(rootPath, fix);
        CheckOrderCommand.PrintResults(hasIssues, messages);
        if (hasIssues)
        {
            Environment.ExitCode = 1;
        }
    }

    private static void RunAll(string rootPath)
    {
        var items = WorkItemStore.LoadAll(rootPath);
        var settings = WorkItemStore.LoadSettings(rootPath);
        var anyFailed = false;

        var (broken, numericRefs) = CheckLinksCommand.Execute(rootPath, items, false);
        var brokenDeps = settings.DependsOnEnabled
            ? CheckLinksCommand.ValidateDependsOn(items)
            : null;

        CheckLinksCommand.PrintResults(broken, numericRefs, brokenDeps);
        if (broken.Count > 0 || numericRefs.Count > 0 || (brokenDeps != null && brokenDeps.Count > 0))
        {
            anyFailed = true;
        }

        var (hasOrderIssues, orderMessages) = CheckOrderCommand.Execute(rootPath, false);
        CheckOrderCommand.PrintResults(hasOrderIssues, orderMessages);
        if (hasOrderIssues)
        {
            anyFailed = true;
        }

        if (anyFailed)
        {
            Environment.ExitCode = 1;
        }
    }
}
