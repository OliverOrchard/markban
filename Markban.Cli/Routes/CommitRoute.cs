public class CommitRoute : CommandRoute
{
    public override bool TryRoute(string[] args, string rootPath)
    {
        if (!args.Contains("--commit"))
            return false;

        var idx = Array.IndexOf(args, "--commit");
        if (idx + 1 >= args.Length)
        {
            PrintUsage();
            return true;
        }

        var identifier = args[idx + 1];

        string? tag = null;
        if (args.Contains("--tag"))
        {
            var ti = Array.IndexOf(args, "--tag");
            if (ti + 1 < args.Length) tag = args[ti + 1];
        }

        string? message = null;
        if (args.Contains("--message"))
        {
            var mi = Array.IndexOf(args, "--message");
            if (mi + 1 < args.Length) message = args[mi + 1];
        }

        if (string.IsNullOrWhiteSpace(tag) || string.IsNullOrWhiteSpace(message))
        {
            PrintUsage();
            return true;
        }

        bool dryRun = args.Contains("--dry-run");

        CommitCommand.ExecuteAsync(rootPath, identifier, tag, message, dryRun).GetAwaiter().GetResult();
        return true;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage: --commit <id|slug> --tag <tag> --message \"message\" [--dry-run]");
        Console.Error.WriteLine($"Valid tags: {string.Join(", ", CommitCommand.ValidTags)}");
    }
}
