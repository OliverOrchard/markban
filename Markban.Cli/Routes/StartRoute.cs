public class StartRoute : CommandRoute
{
    public override string? SubCommand => "start";

    public override HelpEntry Help => new HelpEntry(
        "start <id> [--no-pull] [--dry-run]",
        "Start work on an item — moves it to next active lane; creates a feature branch if enabled",
        "  <id>          work item ID or slug to start\n" +
        "  --no-pull     skip git pull before branching (feature branch mode only)\n" +
        "  --dry-run     show what would happen without making changes");

    private static readonly HashSet<string> KnownFlags = new(StringComparer.OrdinalIgnoreCase)
        { "--no-pull", "--dry-run" };

    private static readonly HashSet<string> ValueFlags = new(StringComparer.OrdinalIgnoreCase);

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "start")
        {
            return false;
        }

        if (args.Length < 2 || args[1].StartsWith("--"))
        {
            PrintHelp();
            return true;
        }

        string? unknownFlag = FindUnknownFlag(args, 2, KnownFlags, ValueFlags);
        if (unknownFlag != null)
        {
            Console.Error.WriteLine($"Unknown flag '{unknownFlag}'.");
            PrintHelp();
            return true;
        }

        string identifier = args[1];
        bool noPull = args.Contains("--no-pull", StringComparer.OrdinalIgnoreCase);
        bool dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);

        var result = StartCommand.ExecuteAsync(rootPath, identifier, noPull, dryRun).GetAwaiter().GetResult();
        if (!result.Success)
        {
            Environment.ExitCode = 1;
        }

        return true;
    }
}
