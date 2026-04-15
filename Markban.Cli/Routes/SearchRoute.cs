public class SearchRoute : CommandRoute
{
    public override string? SubCommand => "search";

    public override HelpEntry Help => new HelpEntry(
        "search <term> [--full]",
        "Ranked search across slugs and IDs (--full scans body content)",
        "  <term>    search term (matches slugs, IDs, and titles)\n" +
        "  --full    also scan file body content");

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "search")
            return false;

        if (args.Length < 2)
        {
            PrintHelp();
            return true;
        }

        var term = args[1];
        var deep = args.Contains("--full");
        ListCommand.ExecuteSearch(rootPath, term, deep);
        return true;
    }
}
