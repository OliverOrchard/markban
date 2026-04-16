public class ShowRoute : CommandRoute
{
    public override string? SubCommand => "show";

    public override HelpEntry Help => new HelpEntry(
        "show <id|slug>",
        "Show a specific work item by ID or slug");

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "show")
        {
            return false;
        }

        if (args.Length < 2)
        {
            PrintHelp();
            return true;
        }

        ListCommand.ExecuteShow(rootPath, args[1]);
        return true;
    }
}
