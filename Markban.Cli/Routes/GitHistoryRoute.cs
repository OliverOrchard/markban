public class GitHistoryRoute : CommandRoute
{
    public override string? SubCommand => "git-history";

    public override HelpEntry Help => new HelpEntry(
        "git-history <file>",
        "Show work item activity from git history of a file");

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "git-history")
        {
            return false;
        }

        if (args.Length < 2)
        {
            Console.Error.WriteLine("Error: git-history requires a file path argument.");
            return true;
        }

        var filePath = args[1];

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
