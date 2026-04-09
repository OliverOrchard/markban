public class OverviewRoute : CommandRoute
{
    public override bool TryRoute(string[] args, string rootPath)
    {
        if (!args.Contains("--overview"))
            return false;

        OverviewCommand.Execute(rootPath);
        return true;
    }
}
