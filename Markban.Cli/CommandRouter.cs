public static class CommandRouter
{
    public static readonly IReadOnlyList<CommandRoute> Routes =
    [
        new WebRoute(),
        new HelpRoute(),
        new ListRoute(),
        new MoveRoute(),
        new NextIdRoute(),
        new ReorderRoute(),
        new CreateRoute(),
        new OverviewRoute(),
        new SanitizeRoute(),
        new CheckLinksRoute(),
        new ReferencesRoute(),
        new GitHistoryRoute(),
        new CommitRoute(),
    ];

    public static bool Route(string[] args, string rootPath)
    {
        foreach (var route in Routes)
        {
            if (route.TryRoute(args, rootPath))
                return true;
        }
        return false;
    }
}
