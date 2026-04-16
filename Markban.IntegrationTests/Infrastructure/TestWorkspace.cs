using System.Text;

namespace Markban.IntegrationTests.Infrastructure;

public record TestLaneConfig(string Name, bool Ordered, string? Type = null, bool Pickable = true);

public class TestWorkspace : IDisposable
{
    public string Root { get; }

    private readonly List<string> _extraDirs = [];

    public TestWorkspace() : this(["Todo", "In Progress", "Testing", "Done", "ideas", "Rejected"]) { }

    public TestWorkspace(IReadOnlyList<string> laneNames)
    {
        Root = Path.Combine(Path.GetTempPath(), "wi-test-" + Guid.NewGuid().ToString("N")[..8]);
        foreach (var lane in laneNames)
        {
            Directory.CreateDirectory(Path.Combine(Root, lane));
        }
    }

    public void AddItem(string folder, string fileName, string content)
    {
        var path = Path.Combine(Root, folder, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
    }

    public bool FileExists(string folder, string fileName)
        => File.Exists(Path.Combine(Root, folder, fileName));

    public string[] GetFiles(string folder)
    {
        var path = Path.Combine(Root, folder);
        return Directory.Exists(path)
            ? Directory.GetFiles(path, "*.md").Select(Path.GetFileName).OfType<string>().Order().ToArray()
            : [];
    }

    public string ReadFile(string folder, string fileName)
        => File.ReadAllText(Path.Combine(Root, folder, fileName));

    /// <summary>
    /// Writes a markban.json alongside the workspace so the CLI can discover it
    /// via config when run without --root. The lanes array drives which directories
    /// the CLI treats as board lanes.
    /// </summary>
    public string WriteConfig(IReadOnlyList<TestLaneConfig> lanes)
    {
        var projectDir = CreateProjectDir();
        var lanesJson = string.Join(",", lanes.Select(l =>
        {
            var parts = new List<string> { $"\"name\":\"{l.Name}\"", $"\"ordered\":{(l.Ordered ? "true" : "false")}" };
            if (l.Type != null)
            {
                parts.Add($"\"type\":\"{l.Type}\"");
            }

            if (!l.Pickable)
            {
                parts.Add("\"pickable\":false");
            }

            return "{" + string.Join(",", parts) + "}";
        }));
        var rootForJson = Root.Replace("\\", "/");
        File.WriteAllText(
            Path.Combine(projectDir, "markban.json"),
            $"{{\"rootPath\":\"{rootForJson}\",\"lanes\":[{lanesJson}]}}");
        return projectDir;
    }

    /// <summary>
    /// Creates a new TestWorkspace with only the specified lane directories,
    /// plus a markban.json in a sibling project dir pointing to it.
    /// </summary>
    public static (TestWorkspace Workspace, string ProjectDir) CreateWithLanes(
        IReadOnlyList<TestLaneConfig> lanes)
    {
        var ws = new TestWorkspace(lanes.Select(l => l.Name).ToArray());
        var projectDir = ws.WriteConfig(lanes);
        return (ws, projectDir);
    }


    /// <summary>
    /// Creates a sibling "project directory" containing a markban.json that points to this workspace's Root.
    /// Use this when you want to test config-file discovery: run the CLI from the returned directory
    /// without passing --root, and FindRoot() will pick up the rootPath from the config.
    /// </summary>
    public string CreateProjectDir(string? customRootPath = null)
    {
        var projectDir = Path.Combine(Path.GetTempPath(), "mb-proj-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(projectDir);
        _extraDirs.Add(projectDir);

        // Use forward slashes so JSON is portable; Path.GetFullPath handles them on Windows too.
        var configRoot = customRootPath ?? Root.Replace("\\", "/");
        File.WriteAllText(
            Path.Combine(projectDir, "markban.json"),
            $$"""{"rootPath": "{{configRoot}}"}""");
        return projectDir;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
        {
            Directory.Delete(Root, true);
        }

        foreach (var dir in _extraDirs)
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
