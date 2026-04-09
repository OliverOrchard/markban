public class ListRoute : CommandRoute
{
    public override bool TryRoute(string[] args, string rootPath)
    {
        if (!args.Contains("--list") && !args.Contains("-l") && !args.Contains("--json") &&
            !args.Contains("--id") && !args.Contains("--slug") && !args.Contains("--search") &&
            !args.Contains("--next"))
            return false;

        ListCommand.Execute(args, rootPath);
        return true;
    }
}
