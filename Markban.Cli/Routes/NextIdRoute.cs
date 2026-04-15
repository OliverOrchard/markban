public class NextIdRoute : CommandRoute
{
    public override string? SubCommand => "next-id";

    public override HelpEntry Help => new HelpEntry(
        "next-id",
        "Print the next safe work item number (max+1)");

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "next-id")
            return false;

        ListCommand.ExecuteNextId(rootPath);
        return true;
    }
}
