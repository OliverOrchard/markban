public class NextIdRoute : CommandRoute
{
    public override bool TryRoute(string[] args, string rootPath)
    {
        if (!args.Contains("--next-id"))
            return false;

        ListCommand.ExecuteNextId(rootPath);
        return true;
    }
}
