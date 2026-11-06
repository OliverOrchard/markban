/// <summary>Shared CLI argument parsing and output handling for <see cref="UndoRoute"/> and <see cref="RedoRoute"/>.</summary>
internal static class HistoryRouteDispatcher
{
    private const int FailureExitCode = 1;

    private static readonly HashSet<string> KnownFlags =
        new(StringComparer.OrdinalIgnoreCase) { "--dry-run", "--list" };

    public static bool TryDispatch(
        CommandRoute route,
        string subCommand,
        string[] args,
        string rootPath,
        Func<string, bool, HistoryCommandResult> execute)
    {
        if (args.Length == 0 || args[0] != subCommand)
        {
            return false;
        }

        if (!ValidateArguments(route, args))
        {
            return true;
        }

        var result = args.Contains("--list", StringComparer.OrdinalIgnoreCase)
            ? HistoryListCommand.Execute(rootPath)
            : execute(rootPath, args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase));
        WriteResult(result);
        return true;
    }

    private static bool ValidateArguments(CommandRoute route, string[] args)
    {
        string? unknownFlag = FindUnknown(args);
        if (unknownFlag is not null)
        {
            Console.Error.WriteLine($"Unknown flag '{unknownFlag}'.");
            route.PrintHelp();
            Environment.ExitCode = FailureExitCode;
            return false;
        }

        if (args.Skip(1).Any(a => !a.StartsWith('-')))
        {
            route.PrintHelp();
            Environment.ExitCode = FailureExitCode;
            return false;
        }

        return true;
    }

    private static string? FindUnknown(string[] args)
    {
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i].StartsWith('-') && !KnownFlags.Contains(args[i]))
            {
                return args[i];
            }
        }

        return null;
    }

    private static void WriteResult(HistoryCommandResult result)
    {
        if (!result.IsSuccess)
        {
            Console.Error.WriteLine(result.Message);
            Environment.ExitCode = FailureExitCode;
            return;
        }

        Console.WriteLine(result.Message);
    }
}
