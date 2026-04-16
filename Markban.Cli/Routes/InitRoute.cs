public class InitRoute : CommandRoute
{
    public override string? SubCommand => "init";

    public override HelpEntry Help => new HelpEntry(
        "init [--path <dir>] [--name \"Board Name\"] [--dry-run]",
        "Scaffold a new board in the current directory",
        "  --path <dir>       use a custom directory instead of work-items/\n" +
        "  --name \"name\"      set a display name in markban.json\n" +
        "  --dry-run          preview without touching the filesystem");

    private static readonly HashSet<string> KnownFlags = new(StringComparer.OrdinalIgnoreCase)
        { "--path", "--name", "--dry-run" };

    private static readonly HashSet<string> ValueFlags = new(StringComparer.OrdinalIgnoreCase)
        { "--path", "--name" };

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "init")
        {
            return false;
        }

        var unknownFlag = FindUnknownFlag(args, 1, KnownFlags, ValueFlags);
        if (unknownFlag != null)
        {
            Console.Error.WriteLine($"Unknown flag '{unknownFlag}'.");
            PrintHelp();
            return true;
        }

        string? boardPath = null;
        if (args.Contains("--path"))
        {
            var i = Array.IndexOf(args, "--path");
            if (i + 1 < args.Length)
            {
                boardPath = args[i + 1];
            }
        }

        string? name = null;
        if (args.Contains("--name"))
        {
            var i = Array.IndexOf(args, "--name");
            if (i + 1 < args.Length)
            {
                name = args[i + 1];
            }
        }

        bool dryRun = args.Contains("--dry-run");

        InitCommand.Execute(Directory.GetCurrentDirectory(), boardPath, name, dryRun);
        return true;
    }
}
