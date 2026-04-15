public class ListRoute : CommandRoute
{
    public override string? SubCommand => "list";

    public override HelpEntry Help => new HelpEntry(
        "list [--folder <lane>] [--summary]",
        "List work items (default: all lanes, full JSON)",
        "  --folder <lane>    filter to a specific lane (Todo, In Progress, Testing, Done, Ideas, Rejected)\n" +
        "  -f                 short for --folder\n" +
        "  --summary          compact output (id, slug, status only)\n" +
        "  -s                 short for --summary");

    private static readonly HashSet<string> KnownFlags = new(StringComparer.OrdinalIgnoreCase)
        { "--folder", "-f", "--summary", "-s" };

    private static readonly HashSet<string> ValueFlags = new(StringComparer.OrdinalIgnoreCase)
        { "--folder", "-f" };

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "list")
            return false;

        var unknownFlag = FindUnknownFlag(args, 1, KnownFlags, ValueFlags);
        if (unknownFlag != null)
        {
            Console.Error.WriteLine($"Unknown flag '{unknownFlag}'.");
            PrintHelp();
            return true;
        }

        ListCommand.Execute(args, rootPath);
        return true;
    }
}
