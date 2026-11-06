public class LaneAddRoute : CommandRoute
{
    private static readonly HashSet<string> KnownFlags =
        new(StringComparer.OrdinalIgnoreCase) { "--dry-run", "--unordered", "--type", "--not-pickable" };

    private static readonly HashSet<string> ValueFlags =
        new(StringComparer.OrdinalIgnoreCase) { "--type" };

    public override string? SubCommand => "lane";

    public override HelpEntry Help => new HelpEntry(
        "lane add <name> [--unordered] [--type <ready|done>] [--not-pickable] [--dry-run]",
        "Add a lane to markban.json and create its directory",
        "  <name>             required - new lane name\n" +
        "  --unordered        lane is not priority-ordered (default: ordered)\n" +
        "  --type <type>      mark as the 'ready' or 'done' lane (one of each allowed)\n" +
        "  --not-pickable     exclude from 'markban next'\n" +
        "  --dry-run          preview without executing");

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length < 2 || args[0] != "lane" || args[1] != "add")
        {
            return false;
        }

        string? error = ParseArgs(args, out string? name, out LaneAddOptions? options);
        if (error != null)
        {
            Console.Error.WriteLine(error);
            PrintHelp();
            Environment.ExitCode = 1;
            return true;
        }

        bool dryRun = args.Skip(2).Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
        WriteResult(LaneAddCommand.Execute(rootPath, name!, options!, dryRun));
        return true;
    }

    private static string? ParseArgs(string[] args, out string? name, out LaneAddOptions? options)
    {
        name = null;
        options = null;
        string? unknownFlag = FindUnknownFlag(args, 2, KnownFlags, ValueFlags);
        if (unknownFlag != null)
        {
            return $"Unknown flag '{unknownFlag}'.";
        }

        int typeIndex = Array.FindIndex(args, 2, a => a.Equals("--type", StringComparison.OrdinalIgnoreCase));
        string? type = typeIndex >= 0 && typeIndex + 1 < args.Length ? args[typeIndex + 1] : null;
        if (typeIndex >= 0 && (type == null || type.StartsWith('-')))
        {
            return "Flag '--type' requires a value (ready or done).";
        }

        var positional = PositionalArgs(args, typeIndex);
        if (positional.Count != 1)
        {
            return "Error: 'lane add' requires exactly one <name>.";
        }

        name = positional[0];
        options = new LaneAddOptions(
            Ordered: !args.Contains("--unordered", StringComparer.OrdinalIgnoreCase),
            Type: type,
            Pickable: !args.Contains("--not-pickable", StringComparer.OrdinalIgnoreCase));
        return null;
    }

    private static List<string> PositionalArgs(string[] args, int typeIndex)
        => args.Select((a, i) => (a, i))
            .Where(x => x.i >= 2 && !x.a.StartsWith('-') && x.i != typeIndex + 1)
            .Select(x => x.a)
            .ToList();

    private static void WriteResult(LaneAddResult result)
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
