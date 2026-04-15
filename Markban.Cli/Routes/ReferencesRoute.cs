public class ReferencesRoute : CommandRoute
{
    public override string? SubCommand => "references";

    public override HelpEntry Help => new HelpEntry(
        "references <slug|id> [--include-ideas]",
        "List all work items that reference [slug]",
        "  <slug|id>         required - item to find references to\n" +
        "  --include-ideas   also search Ideas and Rejected lanes");

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "references")
            return false;

        if (args.Length < 2)
        {
            Console.Error.WriteLine("Error: references requires a slug or ID argument.");
            return true;
        }

        var target = args[1];
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
