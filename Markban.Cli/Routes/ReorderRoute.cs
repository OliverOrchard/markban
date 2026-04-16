public class ReorderRoute : CommandRoute
{
    public override string? SubCommand => "reorder";

    public override HelpEntry Help => new HelpEntry(
        "reorder <lane> <order> [--no-sub-items] [--dry-run]",
        "Reorder items within a lane by comma-separated IDs",
        "  <lane>             target lane (e.g. Todo, \"In Progress\")\n" +
        "  <order>            comma-separated IDs, highest priority first\n" +
        "  --no-sub-items     exclude sub-items from reorder\n" +
        "  --dry-run          preview without executing");

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "reorder")
        {
            return false;
        }

        if (args.Length < 3)
        {
            PrintHelp();
            return true;
        }

        var folder = args[1];
        var orderArg = args[2];
        var noSubItems = args.Contains("--no-sub-items");
        var dryRun = args.Contains("--dry-run");
        ReorderCommand.Execute(rootPath, folder, orderArg, noSubItems, dryRun);
        return true;
    }
}
