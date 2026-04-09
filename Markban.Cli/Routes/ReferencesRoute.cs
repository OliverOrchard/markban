public class ReferencesRoute : CommandRoute
{
    public override bool TryRoute(string[] args, string rootPath)
    {
        if (!args.Contains("--references"))
            return false;

        var idx = Array.IndexOf(args, "--references");
        if (idx + 1 >= args.Length)
        {
            Console.Error.WriteLine("Error: --references requires a slug or ID argument.");
            return true;
        }

        var target = args[idx + 1];
        var includeIdeas = args.Contains("--include-ideas");
        var items = WorkItemStore.LoadAll(rootPath);

        var targetSlug = ReferencesCommand.ResolveToSlug(target, items);
        if (targetSlug == null)
        {
            Console.Error.WriteLine($"Error: could not resolve '{target}' to a known work item.");
            return true;
        }

        var references = ReferencesCommand.Execute(rootPath, items, targetSlug, includeIdeas);
        ReferencesCommand.PrintResults(targetSlug, references);
        return true;
    }
}
