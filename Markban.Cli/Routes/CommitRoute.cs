public class CommitRoute : CommandRoute
{
    public override string? SubCommand => "commit";

    public override HelpEntry Help => new HelpEntry(
        "commit <id[,id2...]> --tag <tag> --message \"msg\" [--dry-run]",
        "Move item(s) to Done, then git add / commit / push",
        "  <id[,id2...]>      required - one or more item IDs/slugs (comma or space separated)\n" +
        $"  --tag <tag>        valid: {string.Join(", ", CommitCommand.ValidTags)}\n" +
        "  --message \"msg\"    commit message\n" +
        "  --dry-run          preview without executing");

    public override HelpEntry GetHelp(string rootPath)
    {
        var tags = CommitCommand.GetValidTags(rootPath);
        return new HelpEntry(
            "commit <id[,id2...]> --tag <tag> --message \"msg\" [--dry-run]",
            "Move item(s) to Done, then git add / commit / push",
            "  <id[,id2...]>      required - one or more item IDs/slugs (comma or space separated)\n" +
            $"  --tag <tag>        valid: {string.Join(", ", tags)}\n" +
            "  --message \"msg\"    commit message\n" +
            "  --dry-run          preview without executing");
    }

    private static readonly HashSet<string> KnownFlags = new(StringComparer.OrdinalIgnoreCase)
        { "--tag", "--message", "--dry-run" };

    private static readonly HashSet<string> ValueFlags = new(StringComparer.OrdinalIgnoreCase)
        { "--tag", "--message" };

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "commit")
        {
            return false;
        }

        if (args.Length < 2)
        {
            PrintHelp();
            return true;
        }

        var (identifiers, argsConsumed) = CollectIdentifiers(args, 1);
        if (identifiers.Count == 0)
        {
            PrintHelp();
            return true;
        }

        var flagStart = 1 + argsConsumed;
        var unknownFlag = FindUnknownFlag(args, flagStart, KnownFlags, ValueFlags);
        if (unknownFlag != null)
        {
            Console.Error.WriteLine($"Unknown flag '{unknownFlag}'.");
            PrintHelp();
            return true;
        }

        string? tag = null;
        if (args.Contains("--tag"))
        {
            var ti = Array.IndexOf(args, "--tag");
            if (ti + 1 < args.Length)
            {
                tag = args[ti + 1];
            }
        }

        string? message = null;
        if (args.Contains("--message"))
        {
            var mi = Array.IndexOf(args, "--message");
            if (mi + 1 < args.Length)
            {
                message = args[mi + 1];
            }
        }

        if (string.IsNullOrWhiteSpace(tag) || string.IsNullOrWhiteSpace(message))
        {
            PrintHelp();
            return true;
        }

        bool dryRun = args.Contains("--dry-run");

        CommitCommand.ExecuteAsync(rootPath, identifiers, tag, message, dryRun).GetAwaiter().GetResult();
        return true;
    }

    private static (IReadOnlyList<string> Ids, int ArgsConsumed) CollectIdentifiers(string[] args, int startIndex)
    {
        var ids = new List<string>();
        int argsConsumed = 0;
        for (int i = startIndex; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg.StartsWith('-'))
            {
                break;
            }

            argsConsumed++;
            // Support comma-separated and space-separated IDs in a single arg
            ids.AddRange(arg.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries));
        }

        return (ids, argsConsumed);
    }
}
