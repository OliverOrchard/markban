public class ListRoute : CommandRoute
{
    public override string? SubCommand => "list";

    public override HelpEntry Help => new HelpEntry(
        "list [--folder <lane>] [--summary] [--filter-tag <tag>]",
        "List work items (default: all lanes, full JSON)",
        "  --folder <lane>       filter to a specific lane (Todo, In Progress, Testing, Done, Ideas, Rejected)\n" +
        "  -f                    short for --folder\n" +
        "  --summary             compact output (id, slug, status only)\n" +
        "  -s                    short for --summary\n" +
        "  --filter-tag <tag>    show only items with this tag (comma-separated for multiple)");

    public override HelpEntry GetHelp(string rootPath)
    {
        var settings = WorkItemStore.LoadSettings(rootPath);
        if (settings.TagsEnabled)
        {
            return Help;
        }

        return new HelpEntry(
            "list [--folder <lane>] [--summary]",
            "List work items (default: all lanes, full JSON)",
            "  --folder <lane>   filter to a specific lane (Todo, In Progress, Testing, Done, Ideas, Rejected)\n" +
            "  -f                short for --folder\n" +
            "  --summary         compact output (id, slug, status only)\n" +
            "  -s                short for --summary");
    }

    private static readonly HashSet<string> KnownFlags = new(StringComparer.OrdinalIgnoreCase)
        { "--folder", "-f", "--summary", "-s", "--filter-tag" };

    private static readonly HashSet<string> ValueFlags = new(StringComparer.OrdinalIgnoreCase)
        { "--folder", "-f", "--filter-tag" };

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "list")
        {
            return false;
        }

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
