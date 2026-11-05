using System.Net;
using System.Net.Http.Json;
using System.Text;
using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class ReorderApiTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly List<string> _tempDirs = [];

    public ReorderApiTests(ToolBuildFixture build)
    {
        _build = build;
    }

    [Fact]
    public async Task PostReorder_RenamesFilesOnDisk_ToMatchRequestedOrder()
    {
        // Arrange
        var boardRoot = CreateBoard("api-reorder-ok");
        AddItem(boardRoot, "1-alpha.md", "# 1 - alpha\n");
        AddItem(boardRoot, "2-beta.md", "# 2 - beta\n");
        AddItem(boardRoot, "3-gamma.md", "# 3 - gamma\n");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);

        // Act
        using var response = await server.Client.PostAsJsonAsync(
            "/api/reorder",
            new { lane = "Todo", orderedIds = new[] { "3", "1", "2" } });
        var files = Directory.GetFiles(Path.Combine(boardRoot, "Todo"), "*.md").Select(Path.GetFileName).Order().ToArray();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        files.Should().Equal(["1-gamma.md", "2-alpha.md", "3-beta.md"],
            because: "the file order on disk must match the dropped order");
        File.ReadAllText(Path.Combine(boardRoot, "Todo", "1-gamma.md")).Should().Contain("# 1 - gamma");
    }

    [Fact]
    public async Task PostReorder_WithUnknownLane_Returns400_AndLeavesFilesUntouched()
    {
        // Arrange
        var boardRoot = CreateBoard("api-reorder-bad");
        AddItem(boardRoot, "1-alpha.md", "# 1 - alpha\n");
        AddItem(boardRoot, "2-beta.md", "# 2 - beta\n");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);

        // Act
        using var response = await server.Client.PostAsJsonAsync(
            "/api/reorder",
            new { lane = "Nope", orderedIds = new[] { "2", "1" } });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        File.Exists(Path.Combine(boardRoot, "Todo", "1-alpha.md")).Should().BeTrue();
    }

    [Fact]
    public async Task PostReorder_WithMissingBody_Returns400()
    {
        // Arrange
        var boardRoot = CreateBoard("api-reorder-empty");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);

        // Act
        using var response = await server.Client.PostAsJsonAsync("/api/reorder", new { lane = "Todo" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs.Where(Directory.Exists))
        {
            Directory.Delete(dir, true);
        }
    }

    private string CreateBoard(string prefix)
    {
        var parentDir = Path.Combine(Path.GetTempPath(), $"mb-api-{prefix}-" + Guid.NewGuid().ToString("N")[..8]);
        var boardRoot = Path.Combine(parentDir, "board");
        _tempDirs.Add(parentDir);
        foreach (var lane in new[] { "Todo", "In Progress", "Testing", "Done", "Ideas", "Rejected" })
        {
            Directory.CreateDirectory(Path.Combine(boardRoot, lane));
        }

        var rootForJson = boardRoot.Replace("\\", "/");
        File.WriteAllText(Path.Combine(parentDir, "markban.json"), $$"""{"rootPath":"{{rootForJson}}"}""", new UTF8Encoding(false));
        return boardRoot;
    }

    private static void AddItem(string boardRoot, string fileName, string content)
    {
        File.WriteAllText(Path.Combine(boardRoot, "Todo", fileName), content, new UTF8Encoding(false));
    }
}
