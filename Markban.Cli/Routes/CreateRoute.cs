public class CreateRoute : CommandRoute
{
    public override string? SubCommand => "create";

    public override HelpEntry Help => new HelpEntry(
        "create \"Title\" [--lane <lane>] [--after <id>] [--priority]",
        "Create a new work item (--sub-item --parent <id> for sub-items)",
        "  \"Title\"             required - work item title\n" +
        "  --lane <lane>      target lane (default: Todo)\n" +
        "  --after <id>       insert after this item ID\n" +
        "  --priority         insert at top of the lane\n" +
        "  --sub-item         create as sub-item (requires --parent)\n" +
        "  --parent <id>      parent item ID when using --sub-item");

    private static readonly HashSet<string> KnownFlags = new(StringComparer.OrdinalIgnoreCase)
        { "--lane", "--after", "--priority", "--sub-item", "--parent" };

    private static readonly HashSet<string> ValueFlags = new(StringComparer.OrdinalIgnoreCase)
        { "--lane", "--after", "--parent" };

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "create")
            return false;

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

        var title = args[1];
        var lane = "Todo";
        if (args.Contains("--lane"))
        {
            var li = Array.IndexOf(args, "--lane");
            if (li + 1 < args.Length)
                lane = args[li + 1];
        }

        string? afterId = null;
        if (args.Contains("--after"))
        {
            var ai = Array.IndexOf(args, "--after");
            if (ai + 1 < args.Length)
                afterId = args[ai + 1];
        }

        bool topPriority = args.Contains("--priority");

        if (args.Contains("--sub-item"))
        {
            string? parentId = null;
            if (args.Contains("--parent"))
            {
                var pi = Array.IndexOf(args, "--parent");
                if (pi + 1 < args.Length)
                    parentId = args[pi + 1];
            }
            if (string.IsNullOrEmpty(parentId))
            {
                Console.WriteLine("Error: --sub-item requires --parent <id> (e.g. --parent 102).");
                return true;
            }
            CreateCommand.ExecuteSubItem(rootPath, title, parentId, lane, afterId);
        }
        else
        {
            CreateCommand.Execute(rootPath, title, lane, afterId, topPriority);
        }
        return true;
    }
}
