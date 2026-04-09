public class HelpRoute : CommandRoute
{
    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length > 0 && !args.Contains("--help") && !args.Contains("-h"))
            return false;

        HelpCommand.Execute();
        return true;
    }
}
