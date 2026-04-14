using System.Text;

namespace Markban.IntegrationTests.Infrastructure;

public class TestWorkspace : IDisposable
{
    public string Root { get; }

    private readonly List<string> _extraDirs = [];

    public TestWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "wi-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(Root, "Todo"));
        Directory.CreateDirectory(Path.Combine(Root, "In Progress"));
        Directory.CreateDirectory(Path.Combine(Root, "Testing"));
        Directory.CreateDirectory(Path.Combine(Root, "Done"));
        Directory.CreateDirectory(Path.Combine(Root, "ideas"));
        Directory.CreateDirectory(Path.Combine(Root, "Rejected"));
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
            Directory.Delete(Root, true);
        foreach (var dir in _extraDirs)
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
    }
}
