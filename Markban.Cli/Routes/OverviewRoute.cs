public class OverviewRoute : CommandRoute
{
    public override string? SubCommand => "overview";

    public override HelpEntry Help => new HelpEntry(
        "overview",
        "Print a compact project progress summary");

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "overview")
        {
            return false;
        }

        OverviewCommand.Execute(rootPath);
        return true;
    }
}
