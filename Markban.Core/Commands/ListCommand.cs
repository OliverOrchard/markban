using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

public static class ListCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void Execute(string[] args, string rootPath)
    {
        var items = WorkItemStore.LoadAll(rootPath);

        if (args.Contains("--folder") || args.Contains("-f"))
        {
            var fi = Array.FindIndex(args, a => a == "--folder" || a == "-f");
            if (fi >= 0 && fi + 1 < args.Length)
            {
                var folderArg = args[fi + 1];
                var validFolders = new[] { "Todo", "In Progress", "Testing", "Done", "Ideas", "Rejected" };
                var normalized = validFolders.FirstOrDefault(f =>
                    f.Equals(folderArg, StringComparison.OrdinalIgnoreCase) ||
                    f.Replace(" ", "").Equals(folderArg.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
                if (normalized != null)
                    items = items.Where(i => i.Status == normalized).ToList();
                else
                    Console.Error.WriteLine($"Warning: unknown folder '{folderArg}', showing all lanes (valid: Todo, InProgress, Testing, Done, Ideas, Rejected).");
            }
        }

        if (args.Contains("--summary") || args.Contains("-s"))
        {
            var summaries = items.Select(i => new WorkItemSummary(i.Id, i.Slug, i.Status)).ToList();
            Console.WriteLine(JsonSerializer.Serialize(summaries, JsonOptions));
        }
        else
        {
            Console.WriteLine(JsonSerializer.Serialize(items, JsonOptions));
        }
    }

    public static void ExecuteShow(string rootPath, string identifier)
    {
        var items = WorkItemStore.LoadAll(rootPath);
        var item = items.FirstOrDefault(i => i.Id.Equals(identifier, StringComparison.OrdinalIgnoreCase));
        item ??= items.FirstOrDefault(i => i.Slug.Contains(identifier.Replace(" ", "-").ToLower()));
        Console.WriteLine(JsonSerializer.Serialize(item, JsonOptions));
    }

    public static void ExecuteSearch(string rootPath, string term, bool deep)
    {
        var items = WorkItemStore.LoadAll(rootPath);
        var results = items
            .Select(i => new { Item = i, Score = CalculateSearchScore(i, term.ToLower(), deep) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Item)
            .ToList();
        Console.WriteLine(JsonSerializer.Serialize(results, JsonOptions));
    }

    public static void ExecuteNext(string rootPath)
    {
        var items = WorkItemStore.LoadAll(rootPath);
        var next = items.Where(i => i.Status == "Todo")
            .OrderBy(i => { var m = Regex.Match(i.Id, @"^(\d+)"); return m.Success ? int.Parse(m.Groups[1].Value) : int.MaxValue; })
            .ThenBy(i => { var m = Regex.Match(i.Id, @"^\d+(.*)$"); return m.Success ? m.Groups[1].Value : i.Id; })
            .FirstOrDefault();
        Console.WriteLine(JsonSerializer.Serialize(next, JsonOptions));
    }

    public static void ExecuteNextId(string rootPath)
    {
        var items = WorkItemStore.LoadAll(rootPath);
        var maxId = items
            .Select(i => { var m = Regex.Match(i.Id, @"^(\d+)"); return m.Success ? int.Parse(m.Groups[1].Value) : 0; })
            .DefaultIfEmpty(0)
            .Max();
        Console.WriteLine(maxId + 1);
    }

    internal static int CalculateSearchScore(WorkItem item, string term, bool deep)
    {
        int score = 0;
        var slugNormalized = item.Slug.Replace("-", " ").ToLower();
        var contentNormalized = item.Content.ToLower();
        var words = term.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w.ToLower()).ToArray();

        if (words.Length == 0) return 0;

        // 1. Exact matches (Absolute priority)
        if (item.Id.Equals(term, StringComparison.OrdinalIgnoreCase)) score += 2000;
        if (item.Slug.Equals(term, StringComparison.OrdinalIgnoreCase) || slugNormalized.Equals(term)) score += 1000;

        // 2. Slug All-Words Match (High priority for 'refactor controller' matching 'refactor-live-mode-controller')
        bool allWordsInSlug = words.All(w => slugNormalized.Contains(w));
        if (allWordsInSlug) score += 500;

        // 3. Word-by-word Slug points
        int matchedSlugWords = words.Count(w => slugNormalized.Contains(w));
        score += matchedSlugWords * 100;

        // 4. Content All-Words Match (Medium priority)
        bool allWordsInContent = words.All(w => contentNormalized.Contains(w));
        if (allWordsInContent) score += 200;

        // 5. Deep Content points (Only if requested)
        if (deep)
        {
            int matchedContentWords = words.Count(w => contentNormalized.Contains(w));
            score += matchedContentWords * 10;
            if (contentNormalized.Contains(term)) score += 50;
        }

        return score;
    }
}
