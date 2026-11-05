using System.Text;

namespace Markban.IntegrationTests.Infrastructure;

/// <summary>Builds throwaway boards (with an optional subItemReorder setting) for reorder tests.</summary>
public static class ReorderBoardBuilder
{
    private static readonly string[] Lanes = ["Todo", "In Progress", "Testing", "Done", "Ideas", "Rejected"];

    public static readonly (string FileName, string Slug)[] ParentAndSubItem =
    [
        ("1-alpha.md", "alpha"),
        ("2-beta.md", "beta"),
        ("2a-beta-child.md", "beta-child"),
        ("3-gamma.md", "gamma")
    ];

    public static string Create(List<string> tempDirs, string prefix, string? subItemReorder, params (string FileName, string Slug)[] todoItems)
    {
        var parentDir = Path.Combine(Path.GetTempPath(), $"mb-{prefix}-" + Guid.NewGuid().ToString("N")[..8]);
        var boardRoot = Path.Combine(parentDir, "board");
        tempDirs.Add(parentDir);
        foreach (var lane in Lanes)
        {
            Directory.CreateDirectory(Path.Combine(boardRoot, lane));
        }

        WriteConfig(parentDir, boardRoot, subItemReorder);
        foreach (var (fileName, slug) in todoItems)
        {
            var id = fileName[..fileName.IndexOf('-')];
            File.WriteAllText(Path.Combine(boardRoot, "Todo", fileName), $"# {id} - {slug}\n", new UTF8Encoding(false));
        }

        return boardRoot;
    }

    public static string[] TodoFiles(string boardRoot)
    {
        return Directory.GetFiles(Path.Combine(boardRoot, "Todo"), "*.md")
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order()
            .ToArray();
    }

    private static void WriteConfig(string parentDir, string boardRoot, string? subItemReorder)
    {
        var rootForJson = boardRoot.Replace("\\", "/");
        var modeJson = subItemReorder is null ? string.Empty : $",\"subItemReorder\":\"{subItemReorder}\"";
        File.WriteAllText(
            Path.Combine(parentDir, "markban.json"),
            $$"""{"rootPath":"{{rootForJson}}"{{modeJson}}}""",
            new UTF8Encoding(false));
    }
}
