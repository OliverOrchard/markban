public class DependsOnRoute : CommandRoute
{
    public override string? SubCommand => "depends-on";

    public override HelpEntry Help => new HelpEntry(
        "depends-on <id|slug> [<dep-slug>] [--remove <slug>] [--list]",
        "Manage or query item dependencies stored in frontmatter",
        "  depends-on <id> <slug>           add a dependency (idempotent)\n" +
        "  depends-on <id> --remove <slug>  remove a dependency\n" +
        "  depends-on <id>                  list dependencies with their status\n" +
        "  depends-on --list                list all items with unresolved dependencies");

    public override bool IsVisible(string rootPath)
        => WorkItemStore.LoadSettings(rootPath).DependsOnEnabled;

    private static readonly HashSet<string> KnownFlags = new(StringComparer.OrdinalIgnoreCase)
        { "--remove", "--list" };

    private static readonly HashSet<string> ValueFlags = new(StringComparer.OrdinalIgnoreCase)
        { "--remove" };

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "depends-on")
        {
            return false;
        }

        if (args.Length >= 2 && args[1] == "--list")
        {
            DependsOnCommand.ListUnresolved(rootPath);
            return true;
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

        if (args.Contains("--remove"))
        {
            var ri = Array.IndexOf(args, "--remove");
            if (ri + 1 >= args.Length)
            {
                Console.Error.WriteLine("Error: --remove requires a dependency slug.");
                PrintHelp();
                return true;
            }

            DependsOnCommand.RemoveDependency(rootPath, identifier, args[ri + 1]);
            return true;
        }

        if (args.Length >= 3 && !args[2].StartsWith('-'))
        {
            DependsOnCommand.AddDependency(rootPath, identifier, args[2]);
            return true;
        }

        DependsOnCommand.ShowDependencies(rootPath, identifier);
        return true;
    }
}
