public class CheckLinksRoute : CommandRoute
{
    public override bool TryRoute(string[] args, string rootPath)
    {
        if (!args.Contains("--check-links"))
            return false;

        var includeIdeas = args.Contains("--include-ideas");
        var items = WorkItemStore.LoadAll(rootPath);
        var (broken, numericRefs) = CheckLinksCommand.Execute(rootPath, items, includeIdeas);
        CheckLinksCommand.PrintResults(broken, numericRefs);
        return true;
    }
}
