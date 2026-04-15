public abstract class CommandRoute
{
    public abstract bool TryRoute(string[] args, string rootPath);
    public abstract HelpEntry Help { get; }

    public virtual string? SubCommand => null;

    public void PrintHelp()
    {
        Console.WriteLine($"Usage: markban {Help.Usage}");
        Console.WriteLine($"  {Help.Description}");
        if (Help.Detail != null)
        {
            Console.WriteLine();
            Console.WriteLine(Help.Detail);
        }
    }

    protected static string? FindUnknownFlag(
        string[] args, int startIndex, HashSet<string> known, HashSet<string> takesValue)
    {
        for (int i = startIndex; i < args.Length; i++)
        {
            if (!args[i].StartsWith("-")) continue;
            if (known.Contains(args[i]))
            {
                if (takesValue.Contains(args[i])) i++;
                continue;
            }
            return args[i];
        }
        return null;
    }
}
