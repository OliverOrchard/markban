public class LaneRenameRoute : CommandRoute
{
    private static readonly HashSet<string> KnownFlags =
        new(StringComparer.OrdinalIgnoreCase) { "--dry-run" };

    private static readonly HashSet<string> ValueFlags =
        new(StringComparer.OrdinalIgnoreCase);

    public override string? SubCommand => "lane";

    public override HelpEntry Help => new HelpEntry(
        "lane rename <old-name> <new-name> [--dry-run]",
        "Rename a lane directory and update markban.json atomically",
        "  <old-name>         required - existing configured lane name\n" +
        "  <new-name>         required - new lane name\n" +
        "  --dry-run          preview without executing");

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "lane")
        {
            return false;
        }

        if (args.Length < 2)
        {
            PrintHelp();
            return true;
        }

        if (args[1] != "rename")
        {
            Console.Error.WriteLine($"Unknown lane subcommand '{args[1]}'. Valid: rename");
            PrintHelp();
            Environment.ExitCode = 1;
            return true;
        }

        return TryRouteRename(args, rootPath);
    }

    private bool TryRouteRename(string[] args, string rootPath)
    {
        string? unknownFlag = FindUnknownFlag(args, 2, KnownFlags, ValueFlags);
        if (unknownFlag != null)
        {
            Console.Error.WriteLine($"Unknown flag '{unknownFlag}'.");
            PrintHelp();
            Environment.ExitCode = 1;
            return true;
        }

        var positional = args.Skip(2).Where(a => !a.StartsWith('-')).ToList();
        if (positional.Count != 2)
        {
            PrintHelp();
            return true;
        }

        bool dryRun = args.Skip(2).Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
        var result = LaneRenameCommand.Execute(rootPath, positional[0], positional[1], dryRun);
        WriteResult(result);
        return true;
    }

    private static void WriteResult(LaneRenameResult result)
    {
        if (!result.Success)
        {
            Console.Error.WriteLine(result.Message);
            Environment.ExitCode = 1;
            return;
        }

        Console.WriteLine(result.Message);
    }
}
