public class CommitRoute : CommandRoute
{
    public override string? SubCommand => "commit";

    public override HelpEntry Help => new HelpEntry(
        "commit <id|slug> --tag <tag> --message \"msg\" [--dry-run]",
        "Move item to Done, then git add / commit / push",
        "  <id|slug>          required - item to commit\n" +
        $"  --tag <tag>        valid: {string.Join(", ", CommitCommand.ValidTags)}\n" +
        "  --message \"msg\"    commit message\n" +
        "  --dry-run          preview without executing");

    public override HelpEntry GetHelp(string rootPath)
    {
        var tags = CommitCommand.GetValidTags(rootPath);
        return new HelpEntry(
            "commit <id|slug> --tag <tag> --message \"msg\" [--dry-run]",
            "Move item to Done, then git add / commit / push",
            "  <id|slug>          required - item to commit\n" +
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

        var identifier = args[1];

        var unknownFlag = FindUnknownFlag(args, 2, KnownFlags, ValueFlags);
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

        CommitCommand.ExecuteAsync(rootPath, identifier, tag, message, dryRun).GetAwaiter().GetResult();
        return true;
    }

}
