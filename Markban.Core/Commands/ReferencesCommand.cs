using System.Text.RegularExpressions;

public static class ReferencesCommand
{
    public record Reference(string FileName, string Id, string Slug, string Status, int Line);

    public static List<Reference> Execute(string rootPath, List<WorkItem> items, string targetSlug, bool includeIdeas = false)
    {
        var foldersToScan = new List<string> { "Todo", "In Progress", "Testing", "Done" };
        if (includeIdeas)
        {
            foldersToScan.Add("ideas");
            foldersToScan.Add("Rejected");
        }

        var results = new List<Reference>();
        var escapedSlug = Regex.Escape(targetSlug);
        var pattern = new Regex($@"\[{escapedSlug}\]", RegexOptions.IgnoreCase);

        foreach (var folder in foldersToScan)
        {
            var folderPath = Path.Combine(rootPath, folder);
            if (!Directory.Exists(folderPath))
            {
                continue;
            }

            foreach (var file in Directory.GetFiles(folderPath, "*.md"))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.StartsWith("."))
                {
                    continue;
                }

                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!pattern.IsMatch(lines[i]))
                    {
                        continue;
                    }

                    var item = items.FirstOrDefault(it =>
                        it.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));

                    results.Add(new Reference(
                        fileName,
                        item?.Id ?? "",
                        item?.Slug ?? Path.GetFileNameWithoutExtension(fileName),
                        item?.Status ?? folder,
                        i + 1));
                    break; // one hit per file is enough
                }
            }
        }

        return results;
    }

    public static void PrintResults(string targetSlug, List<Reference> references)
    {
        if (references.Count == 0)
        {
            Console.WriteLine($"No references to [{targetSlug}] found.");
            return;
        }

        Console.WriteLine($"Found {references.Count} reference(s) to [{targetSlug}]:\n");
        foreach (var r in references)
        {
            var idPart = string.IsNullOrEmpty(r.Id) ? "" : $"{r.Id}: ";
            Console.WriteLine($"  {idPart}{r.Slug} ({r.Status}) — line {r.Line}");
        }
    }

    public static string? ResolveToSlug(string target, List<WorkItem> items)
    {
        // If it matches an ID exactly, return that item's slug
        var byId = items.FirstOrDefault(i =>
            i.Id.Equals(target, StringComparison.OrdinalIgnoreCase));
        if (byId != null)
        {
            return byId.Slug;
        }

        // If it matches a slug exactly, return it
        var bySlug = items.FirstOrDefault(i =>
            i.Slug.Equals(target, StringComparison.OrdinalIgnoreCase));
        if (bySlug != null)
        {
            return bySlug.Slug;
        }

        // Could be a slug that doesn't exist (agent typed it) \u2014 still allow it
        // so the command can report "no references found" rather than erroring
        if (target.Contains('-'))
        {
            return target;
        }

        return null;
    }
}
