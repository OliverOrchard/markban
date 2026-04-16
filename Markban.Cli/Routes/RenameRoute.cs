public class RenameRoute : CommandRoute
{
    public override string? SubCommand => "rename";

    public override HelpEntry Help => new HelpEntry(
        "rename <id|slug> \"New Title\" [--dry-run]",
        "Rename item -- updates H1, filename, and cross-references",
        "  <id|slug>          required - item to rename\n" +
        "  \"New Title\"        required - new title\n" +
        "  --dry-run          preview without executing");

    private static readonly HashSet<string> KnownFlags =
        new(StringComparer.OrdinalIgnoreCase) { "--dry-run" };

    private static readonly HashSet<string> ValueFlags =
        new(StringComparer.OrdinalIgnoreCase);

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "rename")
        {
            return false;
        }

        if (args.Length < 3 || args[2].StartsWith("--"))
        {
            PrintHelp();
            return true;
        }

        var unknownFlag = FindUnknownFlag(args, 3, KnownFlags, ValueFlags);
        if (unknownFlag != null)
        {
            Console.Error.WriteLine($"Unknown flag '{unknownFlag}'.");
            PrintHelp();
            return true;
        }

        var identifier = args[1];
        var newTitle = args[2];
        bool dryRun = args.Contains("--dry-run");
        RenameCommand.Execute(rootPath, identifier, newTitle, dryRun);
        return true;
    }
}
