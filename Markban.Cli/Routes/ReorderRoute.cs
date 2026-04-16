public class ReorderRoute : CommandRoute
{
    private static readonly HashSet<string> KnownFlags =
        new(StringComparer.OrdinalIgnoreCase) { "--no-sub-items", "--dry-run", "--start-number" };

    private static readonly HashSet<string> ValueFlags =
        new(StringComparer.OrdinalIgnoreCase) { "--start-number" };

    public override string? SubCommand => "reorder";

    public override HelpEntry Help => new HelpEntry(
        "reorder <lane> <order> [--no-sub-items] [--dry-run] [--start-number <n>]",
        "Reorder items within a lane by comma-separated IDs",
        "  <lane>             target lane (e.g. Todo, \"In Progress\")\n" +
        "  <order>            comma-separated IDs, highest priority first\n" +
        "  --no-sub-items     exclude sub-items from reorder\n" +
        "  --dry-run          preview without executing\n" +
        "  --start-number <n> assign the first reordered item this number");

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "reorder")
        {
            return false;
        }

        if (args.Length < 3)
        {
            PrintHelp();
            return true;
        }

        var unknownFlag = FindUnknownFlag(args, 3, KnownFlags, ValueFlags);
        if (unknownFlag != null)
        {
            Console.Error.WriteLine($"Unknown flag '{unknownFlag}'.");
            PrintHelp();
            return true;
        }

        var folder = args[1];
        var orderArg = args[2];
        var noSubItems = args.Contains("--no-sub-items");
        var dryRun = args.Contains("--dry-run");
        var startNumber = ParseStartNumber(args);
        if (startNumber == null && args.Contains("--start-number"))
        {
            return true;
        }

        ReorderCommand.Execute(rootPath, folder, orderArg, noSubItems, dryRun, startNumber);
        return true;
    }

    private int? ParseStartNumber(string[] args)
    {
        for (int i = 3; i < args.Length; i++)
        {
            if (!string.Equals(args[i], "--start-number", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (i + 1 >= args.Length || !int.TryParse(args[i + 1], out var value) || value < 1)
            {
                Console.Error.WriteLine("Error: --start-number requires a positive integer value.");
                PrintHelp();
                return null;
            }

            return value;
        }

        return null;
    }
}
