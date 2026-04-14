using System.Text.Json;
using System.Text.RegularExpressions;

public static class WorkItemStore
{
    public static string FindRoot(string? startDir = null)
    {
        var current = startDir ?? Directory.GetCurrentDirectory();
        while (current != null)
        {
            var configPath = Path.Combine(current, "markban.json");
            if (File.Exists(configPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
                    if (doc.RootElement.TryGetProperty("rootPath", out var el))
                    {
                        var relPath = el.GetString();
                        if (!string.IsNullOrWhiteSpace(relPath))
                            return Path.GetFullPath(Path.Combine(current, relPath));
                    }
                }
                catch (JsonException) { }
            }

            var potential = Path.Combine(current, "work-items");
            if (Directory.Exists(potential)) return potential;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException("Could not find 'work-items' directory in any parent path.");
    }

    public static List<WorkItem> LoadAll(string root)
    {
        var folders = new[] { "Todo", "In Progress", "Testing", "Done" };
        var result = new List<WorkItem>();

        foreach (var folder in folders)
        {
            var path = Path.Combine(root, folder);
            if (!Directory.Exists(path)) continue;

            var folderItems = new List<(WorkItem Item, int Number, string Letter)>();

            foreach (var file in Directory.GetFiles(path, "*.md"))
            {
                var fileName = Path.GetFileName(file);
                var match = Regex.Match(fileName, @"^(\d+)([a-z]?)-(.+)\.md$");
                if (!match.Success) continue;

                var number = int.Parse(match.Groups[1].Value);
                var letter = match.Groups[2].Value;

                folderItems.Add((new WorkItem(
                    Id: match.Groups[1].Value + letter,
                    Slug: match.Groups[3].Value,
                    Status: folder,
                    Content: File.ReadAllText(file),
                    FileName: fileName,
                    FullPath: file
                ), number, letter));
            }

            var ordered = folder == "Done"
                ? folderItems.OrderByDescending(x => x.Number).ThenByDescending(x => x.Letter)
                : folderItems.OrderBy(x => x.Number).ThenBy(x => x.Letter);

            result.AddRange(ordered.Select(x => x.Item));
        }

        // Load ideas/ and Rejected/ separately -- no numbers, just slug-named files
        foreach (var (slugFolder, statusName) in new[] { ("ideas", "Ideas"), ("Rejected", "Rejected") })
        {
            var slugPath = Path.Combine(root, slugFolder);
            if (!Directory.Exists(slugPath)) continue;

            foreach (var file in Directory.GetFiles(slugPath, "*.md").OrderBy(f => f))
            {
                var fileName = Path.GetFileName(file);
                if (fileName.StartsWith(".")) continue;
                result.Add(new WorkItem(
                    Id: "",
                    Slug: Path.GetFileNameWithoutExtension(fileName),
                    Status: statusName,
                    Content: File.ReadAllText(file),
                    FileName: fileName,
                    FullPath: file
                ));
            }
        }

        return result;
    }
}
