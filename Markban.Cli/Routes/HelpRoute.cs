public class HelpRoute : CommandRoute
{
    public override HelpEntry Help => new HelpEntry(
        "help",
        "Show this help");

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length > 0 && args[0] != "help" && !args.Contains("--help") && !args.Contains("-h"))
        {
            return false;
        }

        if (args.Length >= 2 && args[0] == "help")
        {
            var subCmd = args[1];
            var match = CommandRouter.Routes.FirstOrDefault(r =>
                r.SubCommand?.Equals(subCmd, StringComparison.OrdinalIgnoreCase) == true);
            if (match != null)
            {
                match.PrintHelp(rootPath);
                return true;
            }
        }

        HelpCommand.Execute(CommandRouter.Routes
            .Where(r => r.IsVisible(rootPath))
            .Select(r => r.GetHelp(rootPath))
            .ToList());
        return true;
    }
}
