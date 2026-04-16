public class NextRoute : CommandRoute
{
    public override string? SubCommand => "next";

    public override HelpEntry Help => new HelpEntry(
        "next",
        "Show the highest priority Todo item");

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "next")
        {
            return false;
        }

        ListCommand.ExecuteNext(rootPath);
        return true;
    }
}
