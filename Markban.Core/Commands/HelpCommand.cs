public static class HelpCommand
{
    private const int UsageColumnWidth = 52;

    public static void Execute(IReadOnlyList<HelpEntry> entries)
    {
        Console.WriteLine("markban - markdown board CLI");
        Console.WriteLine("Usage:");
        foreach (var entry in entries)
        {
            var usageLine = "  " + entry.Usage;
            if (usageLine.Length >= UsageColumnWidth)
            {
                Console.WriteLine($"{usageLine}  {entry.Description}");
            }
            else
            {
                Console.WriteLine($"{usageLine.PadRight(UsageColumnWidth)}{entry.Description}");
            }
        }
        Console.WriteLine();
        Console.WriteLine("Global flags:");
        Console.WriteLine("  --root <path>                                   Override work-items directory location");
    }
}
