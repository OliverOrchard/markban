public class TagRoute : CommandRoute
{
    public override string? SubCommand => "tag";

    public override HelpEntry Help => new HelpEntry(
        "tag <id|slug> [<tags>] [--remove <tag>]",
        "Add, remove, or list tags on a work item",
        "  tag <id> bug,backend      add tags (comma-separated, idempotent)\n" +
        "  tag <id> --remove bug     remove a tag\n" +
        "  tag <id>                  list tags for the item");

    public override bool IsVisible(string rootPath)
        => WorkItemStore.LoadSettings(rootPath).TagsEnabled;

    private static readonly HashSet<string> KnownFlags = new(StringComparer.OrdinalIgnoreCase)
        { "--remove" };

    private static readonly HashSet<string> ValueFlags = new(StringComparer.OrdinalIgnoreCase)
        { "--remove" };

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "tag")
        {
            return false;
        }

        if (args.Length < 2)
        {
            PrintHelp();
            return true;
        }

        var identifier = args[1];
        var unknownFlag = FindUnknownFlag(args, 2, KnownFlags, ValueFlags);
        if (unknownFlag != null)
        {
            Console.Error.WriteLine($"Unknown flag '{unknownFlag}'.");
            PrintHelp();
            return true;
        }

        // --remove <tag>
        if (args.Contains("--remove"))
        {
            var ri = Array.IndexOf(args, "--remove");
            if (ri + 1 >= args.Length)
            {
                Console.Error.WriteLine("Error: --remove requires a tag name.");
                PrintHelp();
                return true;
            }

            TagCommand.RemoveTag(rootPath, identifier, args[ri + 1]);
            return true;
        }

        // Positional tags argument or list
        if (args.Length < 3 || args[2].StartsWith('-'))
        {
            TagCommand.ListTags(rootPath, identifier);
            return true;
        }

        var tagsToAdd = args[2]
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .ToList();

        if (tagsToAdd.Count == 0)
        {
            TagCommand.ListTags(rootPath, identifier);
            return true;
        }

        TagCommand.AddTags(rootPath, identifier, tagsToAdd);
        return true;
    }
}
