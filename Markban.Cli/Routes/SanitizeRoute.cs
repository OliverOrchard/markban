public class SanitizeRoute : CommandRoute
{
    public override bool TryRoute(string[] args, string rootPath)
    {
        if (!args.Contains("--sanitize"))
            return false;

        var items = WorkItemStore.LoadAll(rootPath);
        SanitizeCommand.Execute(rootPath, items);
        return true;
    }
}
