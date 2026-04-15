public static class CommandRouter
{
    public static readonly IReadOnlyList<CommandRoute> Routes =
    [
        new WebRoute(),
        new HelpRoute(),
        new InitRoute(),
        new ListRoute(),
        new NextRoute(),
        new ShowRoute(),
        new SearchRoute(),
        new MoveRoute(),
        new NextIdRoute(),
        new ReorderRoute(),
        new CreateRoute(),
        new OverviewRoute(),
        new SanitizeRoute(),
        new HealthRoute(),
        new ReferencesRoute(),
        new GitHistoryRoute(),
        new CommitRoute(),
    ];

    public static bool Route(string[] args, string rootPath)
    {
        if (args.Length >= 2 && (args.Contains("-h") || args.Contains("--help")))
        {
            var match = Routes.FirstOrDefault(r => r.SubCommand == args[0]);
            if (match != null)
            {
                match.PrintHelp();
                return true;
            }
        }

        foreach (var route in Routes)
        {
            if (route.TryRoute(args, rootPath))
                return true;
        }
        return false;
    }
}
