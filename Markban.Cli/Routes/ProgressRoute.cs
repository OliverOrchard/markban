public class ProgressRoute : CommandRoute
{
    public override string? SubCommand => "progress";

    public override HelpEntry Help => new HelpEntry(
        "progress <id|slug> [--dry-run]",
        "Advance an item to the next lane in workflow order",
        "  <id|slug>          required - item to advance\n" +
        "  --dry-run          preview without moving");

    private static readonly HashSet<string> KnownFlags =
        new(StringComparer.OrdinalIgnoreCase) { "--dry-run" };

    private static readonly HashSet<string> ValueFlags =
        new(StringComparer.OrdinalIgnoreCase);

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "progress")
        {
            return false;
        }

        if (args.Length < 2 || args[1].StartsWith("--"))
        {
            PrintHelp();
            return true;
        }

        var unknownFlag = FindUnknownFlag(args, 2, KnownFlags, ValueFlags);
        if (unknownFlag != null)
        {
            Console.Error.WriteLine($"Unknown flag '{unknownFlag}'.");
            PrintHelp();
            return true;
        }

        var identifier = args[1];
        bool dryRun = args.Contains("--dry-run");
        ProgressCommand.Execute(rootPath, identifier, dryRun);
        return true;
    }
}
