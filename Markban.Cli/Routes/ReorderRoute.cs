public class ReorderRoute : CommandRoute
{
    public override bool TryRoute(string[] args, string rootPath)
    {
        if (!args.Contains("--reorder"))
            return false;

        var index = Array.IndexOf(args, "--reorder");
        if (index + 2 >= args.Length)
        {
            Console.WriteLine("Usage: --reorder <folder> <order> [--no-sub-items] [--dry-run]");
            Console.WriteLine("  folder: Todo, \"In Progress\", Testing, Done (also accepts InProgress)");
            Console.WriteLine("  order:  comma-separated current item numbers, highest priority first");
            return true;
        }
        var reorderFolder = args[index + 1];
        var reorderOrder = args[index + 2];
        var noSubItems = args.Contains("--no-sub-items");
        var dryRun = args.Contains("--dry-run");
        ReorderCommand.Execute(rootPath, reorderFolder, reorderOrder, noSubItems, dryRun);
        return true;
    }
}
