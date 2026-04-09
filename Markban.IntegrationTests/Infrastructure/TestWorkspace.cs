using System.Text;

namespace Markban.IntegrationTests.Infrastructure;

public class TestWorkspace : IDisposable
{
    public string Root { get; }

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

    public void Dispose()
    {
        if (Directory.Exists(Root))
            Directory.Delete(Root, true);
    }
}
