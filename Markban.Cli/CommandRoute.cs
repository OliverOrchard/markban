public abstract class CommandRoute
{
    public abstract bool TryRoute(string[] args, string rootPath);
    public abstract HelpEntry Help { get; }
    public virtual HelpEntry GetHelp(string rootPath) => Help;

    public virtual string? SubCommand => null;

    public void PrintHelp() => PrintEntry(Help);

    public void PrintHelp(string rootPath) => PrintEntry(GetHelp(rootPath));

    private static void PrintEntry(HelpEntry entry)
    {
        Console.WriteLine($"Usage: markban {entry.Usage}");
        Console.WriteLine($"  {entry.Description}");
        if (entry.Detail != null)
        {
            Console.WriteLine();
            Console.WriteLine(entry.Detail);
        }
    }

    protected static string? FindUnknownFlag(
        string[] args, int startIndex, HashSet<string> known, HashSet<string> takesValue)
    {
        for (int i = startIndex; i < args.Length; i++)
        {
            if (!args[i].StartsWith("-"))
            {
                continue;
            }

            if (known.Contains(args[i]))
            {
                if (takesValue.Contains(args[i]))
                {
                    i++;
                }

                continue;
            }
            return args[i];
        }
        return null;
    }
}
