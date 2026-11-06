public class BoardRoute : CommandRoute
{
    private const string AddSubCommand = "add";
    private const string RemoveSubCommand = "remove";
    private const int FailureExitCode = 1;

    private static readonly HashSet<string> KnownAddFlags = new(StringComparer.OrdinalIgnoreCase)
        { "--no-link", "--copy-config", "--dry-run" };

    private static readonly HashSet<string> KnownRemoveFlags = new(StringComparer.OrdinalIgnoreCase)
        { "--dry-run" };

    public override string? SubCommand => "board";

    public override HelpEntry Help => new HelpEntry(
        "board <add|remove> ...",
        "Manage boards in markban.json",
        "  add <name> <path> [--no-link] [--copy-config] [--dry-run]\n" +
        "                     bootstrap a board directory and register it in markban.json\n" +
        "  remove <key> [--dry-run]\n" +
        "                     remove the board with this key; its directory is left untouched\n" +
        "  --no-link          bootstrap the board without modifying the current root config\n" +
        "  --copy-config      copy board-local settings from the current board\n" +
        "  --dry-run          preview without modifying the filesystem");

    public override bool TryRoute(string[] args, string rootPath)
    {
        if (args.Length == 0 || args[0] != "board")
        {
            return false;
        }

        string configDir = Path.GetDirectoryName(rootPath) ?? rootPath;
        var positional = args.Skip(1).Where(a => !a.StartsWith('-')).ToArray();
        if (positional.Length > 0 && positional[0] == AddSubCommand)
        {
            RouteAdd(args, positional, configDir, rootPath);
            return true;
        }

        if (positional.Length > 0 && positional[0] == RemoveSubCommand)
        {
            RouteRemove(args, positional, configDir, rootPath);
            return true;
        }

        PrintUsageError(FindUnknownFlag(args, 1, KnownAddFlags, []));
        return true;
    }

    private void RouteAdd(string[] args, string[] positional, string configDir, string rootPath)
    {
        string? unknownFlag = FindUnknownFlag(args, 1, KnownAddFlags, []);
        if (unknownFlag != null || positional.Length != 3)
        {
            PrintUsageError(unknownFlag);
            return;
        }

        var options = new BoardAddOptions(
            Name: positional[1],
            BoardPath: positional[2],
            NoLink: args.Contains("--no-link"),
            CopyConfig: args.Contains("--copy-config"),
            DryRun: args.Contains("--dry-run"));
        var result = BoardAddCommand.Execute(configDir, rootPath, options);
        PrintAddResult(result, configDir);
    }

    private void RouteRemove(string[] args, string[] positional, string configDir, string rootPath)
    {
        string? unknownFlag = FindUnknownFlag(args, 1, KnownRemoveFlags, []);
        if (unknownFlag != null || positional.Length != 2)
        {
            PrintUsageError(unknownFlag);
            return;
        }

        var result = BoardRemoveCommand.Execute(configDir, rootPath, positional[1], args.Contains("--dry-run"));
        PrintRemoveResult(result, configDir);
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

    private static void PrintAddResult(BoardAddResult result, string configDir)
    {
        if (result.IsSuccess)
        {
            PrintAddSuccess(result);
            return;
        }

        Console.Error.WriteLine(DescribeAddFailure(result, configDir));
        Environment.ExitCode = FailureExitCode;
    }

    private static void PrintAddSuccess(BoardAddResult result)
    {
        bool dryRun = result.Status == BoardAddStatus.DryRun;
        PrintAddDirectoryStatus(result, dryRun);
        PrintAddLinkStatus(result, dryRun);
    }

    private static void PrintAddDirectoryStatus(BoardAddResult result, bool dryRun)
    {
        if (result.Bootstrapped)
        {
            Console.WriteLine(dryRun
                ? $"Would bootstrap board '{result.BoardName}' at {result.BoardPath}."
                : $"Bootstrapped board '{result.BoardName}' at {result.BoardPath}.");
            return;
        }

        Console.WriteLine(dryRun
            ? $"Would reuse existing board directory at {result.BoardPath}."
            : $"Reused existing board directory at {result.BoardPath}.");
    }

    private static void PrintAddLinkStatus(BoardAddResult result, bool dryRun)
    {
        if (!result.Linked)
        {
            Console.WriteLine(dryRun
                ? "Would skip linking in root markban.json (--no-link)."
                : "Skipped linking in root markban.json (--no-link).");
            return;
        }

        Console.WriteLine(dryRun
            ? $"Would add board '{result.BoardName}' ({result.Key}) to markban.json."
            : $"Added board '{result.BoardName}' ({result.Key}) to markban.json.");
    }

    private static string DescribeAddFailure(BoardAddResult result, string configDir) => result.Status switch
    {
        BoardAddStatus.DuplicateKey =>
            $"Error: A board with key '{result.Key}' already exists in 'boards'.",
        BoardAddStatus.DuplicatePath =>
            $"Error: Board path '{result.BoardPath}' is already registered under key '{result.ExistingKey}'.",
        BoardAddStatus.ConfigMissing => $"Error: No markban.json found in {configDir}.",
        BoardAddStatus.WriteFailed => $"Error: Could not update markban.json: {result.ErrorMessage}",
        _ => $"Error: {result.ErrorMessage}",
    };

    private static void PrintRemoveResult(BoardRemoveResult result, string configDir)
    {
        if (result.IsSuccess)
        {
            PrintRemoveSuccess(result);
            return;
        }

        Console.Error.WriteLine(DescribeRemoveFailure(result, configDir));
        Environment.ExitCode = FailureExitCode;
    }

    private static void PrintRemoveSuccess(BoardRemoveResult result)
    {
        bool dryRun = result.Status == BoardRemoveStatus.DryRun;
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

    private static string DescribeRemoveFailure(BoardRemoveResult result, string configDir) => result.Status switch
    {
        BoardRemoveStatus.RootBoard =>
            $"Error: '{result.Key}' is the root board and cannot be removed from its own config.",
        BoardRemoveStatus.NotFound => $"Error: No board with key '{result.Key}' found in 'boards'.",
        BoardRemoveStatus.ConfigMissing => $"Error: No markban.json found in {configDir}.",
        BoardRemoveStatus.WriteFailed => $"Error: Could not update markban.json: {result.ErrorMessage}",
        _ => $"Error: {result.ErrorMessage}",
    };
}
