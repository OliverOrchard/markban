public class NextRoute : CommandRoute
{
    public override string? SubCommand => "next";

    public override HelpEntry Help => new HelpEntry(
        "next [--include-blocked]",
        "Show the highest priority Todo item",
        "  --include-blocked   include blocked and dependency-blocked items");

    public override HelpEntry GetHelp(string rootPath)
    {
        var settings = WorkItemStore.LoadSettings(rootPath);
        if (settings.BlockedEnabled)
        {
            return Help;
        }

        return new HelpEntry(
            "next",
            "Show the highest priority Todo item",
            null);
    }

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "next")
        {
            return false;
        }

        var includeBlocked = args.Contains("--include-blocked");
        ListCommand.ExecuteNext(rootPath, includeBlocked);
        return true;
    }
}
