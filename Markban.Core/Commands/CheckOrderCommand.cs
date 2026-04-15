using System.Text.RegularExpressions;

public static class CheckOrderCommand
{
    public static (bool HasIssues, List<string> Messages) Execute(string rootPath, bool fix)
    {
        var items = WorkItemStore.LoadAll(rootPath);
        var messages = new List<string>();

        var duplicates = items
            .Where(i => !string.IsNullOrEmpty(i.Id))
            .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1);

        foreach (var dup in duplicates)
            messages.Add($"Duplicate ID '{dup.Key}': {string.Join(", ", dup.Select(i => i.FileName))}");

        var primaryIds = items
            .Where(i => Regex.IsMatch(i.Id, @"^\d+$"))
            .Select(i => int.Parse(i.Id))
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        if (primaryIds.Count > 0)
        {
            for (var n = primaryIds[0] + 1; n < primaryIds[^1]; n++)
            {
                if (!primaryIds.Contains(n))
                    messages.Add($"Gap in global ID sequence: {n} is missing");
            }
        }

        if (fix && messages.Count > 0)
            messages.Add("--fix: Use 'markban reorder <lane> <order>' to resolve ordering issues manually.");

        return (messages.Count > 0, messages);
    }

    public static void PrintResults(bool hasIssues, List<string> messages)
    {
        if (!hasIssues)
        {
            Console.WriteLine("check-order: OK");
            return;
        }

        Console.WriteLine($"check-order: {messages.Count} issue(s) found:");
        foreach (var msg in messages)
            Console.WriteLine($"  {msg}");
    }
}
