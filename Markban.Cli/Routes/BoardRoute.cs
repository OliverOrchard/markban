public class BoardRoute : CommandRoute
{
    private const string RemoveSubCommand = "remove";
    private const int FailureExitCode = 1;

    public override string? SubCommand => "board";

    public override HelpEntry Help => new HelpEntry(
        "board remove <key> [--dry-run]",
        "Unregister a board from the boards array in markban.json",
        "  remove <key>       remove the board with this key; its directory is left untouched\n" +
        "  --dry-run          preview without modifying markban.json");

    private static readonly HashSet<string> KnownFlags = new(StringComparer.OrdinalIgnoreCase)
        { "--dry-run" };

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "board")
        {
            return false;
        }

        var unknownFlag = FindUnknownFlag(args, 1, KnownFlags, []);
        var positional = args.Skip(1).Where(a => !a.StartsWith('-')).ToArray();
        if (unknownFlag != null || positional.Length != 2 || positional[0] != RemoveSubCommand)
        {
            PrintUsageError(unknownFlag);
            return true;
        }

        var configDir = Path.GetDirectoryName(rootPath) ?? rootPath;
        var result = BoardRemoveCommand.Execute(configDir, rootPath, positional[1], args.Contains("--dry-run"));
        PrintResult(result, configDir);
        return true;
    }

    private void PrintUsageError(string? unknownFlag)
    {
        if (unknownFlag != null)
        {
            Console.Error.WriteLine($"Unknown flag '{unknownFlag}'.");
        }

        PrintHelp();
        Environment.ExitCode = FailureExitCode;
    }

    private static void PrintResult(BoardRemoveResult result, string configDir)
    {
        if (result.IsSuccess)
        {
            PrintSuccess(result);
            return;
        }

        Console.Error.WriteLine(DescribeFailure(result, configDir));
        Environment.ExitCode = FailureExitCode;
    }

    private static void PrintSuccess(BoardRemoveResult result)
    {
        var dryRun = result.Status == BoardRemoveStatus.DryRun;
        Console.WriteLine(dryRun
            ? $"Would remove board '{result.BoardName}' ({result.Key}) from markban.json."
            : $"Removed board '{result.BoardName}' ({result.Key}) from markban.json.");
        if (result.BoardsKeyRemoved)
        {
            Console.WriteLine(dryRun
                ? "Would remove the empty 'boards' key (single-board mode)."
                : "Removed the empty 'boards' key (single-board mode).");
        }

        Console.WriteLine($"Board directory left untouched: {result.BoardPath}");
    }

    private static string DescribeFailure(BoardRemoveResult result, string configDir) => result.Status switch
    {
        BoardRemoveStatus.RootBoard =>
            $"Error: '{result.Key}' is the root board and cannot be removed from its own config.",
        BoardRemoveStatus.NotFound => $"Error: No board with key '{result.Key}' found in 'boards'.",
        BoardRemoveStatus.ConfigMissing => $"Error: No markban.json found in {configDir}.",
        BoardRemoveStatus.WriteFailed => $"Error: Could not update markban.json: {result.ErrorMessage}",
        _ => $"Error: {result.ErrorMessage}",
    };
}
