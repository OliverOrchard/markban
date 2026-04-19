public class CycleTimeRoute : CommandRoute
{
    public override string? SubCommand => "cycle-time";

    public override HelpEntry Help => new HelpEntry(
        "cycle-time",
        "Report cycle time for completed work items (requires git history)",
        "  No options — reads git history to compute time-in-progress per item.");

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "cycle-time")
        {
            return false;
        }

        var entries = CycleTimeCommand.ExecuteAsync(rootPath).GetAwaiter().GetResult();
        CycleTimeCommand.PrintResults(entries);
        return true;
    }
}
