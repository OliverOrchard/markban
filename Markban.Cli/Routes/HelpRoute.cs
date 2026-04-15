public class HelpRoute : CommandRoute
{
    public override HelpEntry Help => new HelpEntry(
        "help",
        "Show this help");

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length > 0 && args[0] != "help" && !args.Contains("--help") && !args.Contains("-h"))
            return false;

        HelpCommand.Execute(CommandRouter.Routes.Select(r => r.Help).ToList());
        return true;
    }
}
