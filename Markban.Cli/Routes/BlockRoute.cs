public class BlockRoute : CommandRoute
{
    public override string? SubCommand => "block";

    public override HelpEntry Help => new HelpEntry(
        "block <id|slug> [\"reason\"] | --remove | --list",
        "Mark a work item as blocked, unblock it, or list all blocked items",
        "  block <id> \"reason\"   mark item as blocked with a reason\n" +
        "  block <id> --remove   unblock an item\n" +
        "  block --list          list all blocked items across lanes");

    public override bool IsVisible(string rootPath)
        => WorkItemStore.LoadSettings(rootPath).BlockedEnabled;

    private static readonly HashSet<string> KnownFlags = new(StringComparer.OrdinalIgnoreCase)
        { "--remove", "--list" };

    private static readonly HashSet<string> ValueFlags = new(StringComparer.OrdinalIgnoreCase);

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "block")
        {
            return false;
        }

        if (args.Length >= 2 && args[1] == "--list")
        {
            BlockCommand.ListBlocked(rootPath);
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
            BlockCommand.Unblock(rootPath, identifier);
            return true;
        }

        // Positional reason argument
        var reason = args.Length >= 3 && !args[2].StartsWith('-') ? args[2] : "";
        if (string.IsNullOrWhiteSpace(reason))
        {
            Console.Error.WriteLine("Error: A reason is required. Usage: markban block <id> \"reason\"");
            PrintHelp();
            return true;
        }

        BlockCommand.Block(rootPath, identifier, reason);
        return true;
    }
}
