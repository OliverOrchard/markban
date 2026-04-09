public class GitHistoryRoute : CommandRoute
{
    public override bool TryRoute(string[] args, string rootPath)
    {
        if (!args.Contains("--git-history"))
            return false;

        var idx = Array.IndexOf(args, "--git-history");
        if (idx + 1 >= args.Length)
        {
            Console.Error.WriteLine("Error: --git-history requires a file path argument.");
            return true;
        }

        var filePath = args[idx + 1];

        // Resolve the repo root from the work-items root (go up one level)
        var repoRoot = Path.GetDirectoryName(rootPath);
        if (repoRoot == null)
        {
            Console.Error.WriteLine("Error: Could not determine repository root.");
            return true;
        }

        var events = GitHistoryCommand.ExecuteAsync(repoRoot, filePath).GetAwaiter().GetResult();
        GitHistoryCommand.PrintResults(events, filePath);
        return true;
    }
}
