using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class MultiBoardWebTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly List<string> _tempDirs = [];

    public MultiBoardWebTests(ToolBuildFixture build)
    {
        _build = build;
    }

    [Fact]
    public async Task BoardsEndpoint_WithoutBoardsConfig_ReturnsEmptyArray()
    {
        // Arrange
        var projectDir = CreateTempDir("web-boards-empty");
        var boardRoot = CreateBoardRoot(projectDir, "board");
        File.WriteAllText(Path.Combine(projectDir, "markban.json"), """{"rootPath":"./board"}""");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);

        // Act
        using var response = await server.Client.GetAsync("/api/boards");
        var boards = await DeserializeAsync<List<BoardApiResponse>>(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.CacheControl.Should().NotBeNull();
        response.Headers.CacheControl!.NoCache.Should().BeTrue();
        response.Headers.CacheControl.NoStore.Should().BeTrue();
        boards.Should().BeEmpty();
    }

    [Fact]
    public async Task BoardsEndpoint_WithBoardsConfig_ReturnsConfiguredBoards()
    {
        // Arrange
        var projectDir = CreateMultiBoardProject(
            "web-boards-list",
            [("Backend", "backend-board"), ("Frontend", "frontend-board")]);
        var boardRoot = Path.Combine(projectDir, "main-board");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);

        // Act
        using var response = await server.Client.GetAsync("/api/boards");
        var boards = await DeserializeAsync<List<BoardApiResponse>>(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        boards.Should().BeEquivalentTo(
            [new BoardApiResponse("Backend", "backend"), new BoardApiResponse("Frontend", "frontend")]);
    }

    [Fact]
    public async Task ItemsAndMoveEndpoints_UseSelectedBoard_AndRejectUnknownKeys()
    {
        // Arrange
        var projectDir = CreateMultiBoardProject(
            "web-items-move",
            [("Backend", "backend-board"), ("Frontend", "frontend-board")]);
        var boardRoot = Path.Combine(projectDir, "main-board");
        var backendRoot = Path.Combine(projectDir, "backend-board");
        var frontendRoot = Path.Combine(projectDir, "frontend-board");
        File.WriteAllText(Path.Combine(backendRoot, "Todo", "1-api-task.md"), "# 1 - API Task\n\n## Description\n\nBackend item");
        File.WriteAllText(Path.Combine(frontendRoot, "Todo", "1-ui-task.md"), "# 1 - UI Task\n\n## Description\n\nFrontend item");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);

        // Act
        using var itemsResponse = await server.Client.GetAsync("/api/items?board=BACKEND");
        var items = await DeserializeAsync<List<WorkItemApiResponse>>(itemsResponse);
        using var moveResponse = await server.Client.PostAsJsonAsync("/api/move?board=backend", new MoveApiRequest("1", "In Progress"));
        var moveResult = await DeserializeAsync<JsonElement>(moveResponse);
        using var invalidResponse = await server.Client.GetAsync("/api/items?board=missing");
        var invalidResult = await DeserializeAsync<JsonElement>(invalidResponse);

        // Assert
        itemsResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        items.Should().ContainSingle()
            .Which.Slug.Should().Be("api-task",
                because: "the board query parameter should select the backend board regardless of key casing");
        moveResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        moveResult.GetProperty("message").GetString().Should().Contain("Successfully moved");
        File.Exists(Path.Combine(backendRoot, "In Progress", "1-api-task.md")).Should().BeTrue(
            because: "moves should execute inside the selected board");
        File.Exists(Path.Combine(frontendRoot, "Todo", "1-ui-task.md")).Should().BeTrue(
            because: "moving on one board must not affect another board");
        invalidResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        invalidResult.GetProperty("error").GetString().Should().Contain("Unknown board");
    }

    public void Dispose()
    {
        foreach (var tempDir in _tempDirs)
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    private string CreateTempDir(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        _tempDirs.Add(path);
        return path;
    }

    private string CreateMultiBoardProject(string prefix, IReadOnlyList<(string Name, string Path)> boards)
    {
        var projectDir = CreateTempDir(prefix);
        CreateBoardRoot(projectDir, "main-board");
        foreach (var board in boards)
        {
            CreateBoardRoot(projectDir, board.Path);
        }

        var boardsJson = string.Join(",", boards.Select(board => $"{{\"name\":\"{board.Name}\",\"path\":\"./{board.Path}\"}}"));
        File.WriteAllText(
            Path.Combine(projectDir, "markban.json"),
            $"{{\"rootPath\":\"./main-board\",\"boards\":[{boardsJson}]}}");
        return projectDir;
    }

    private static string CreateBoardRoot(string projectDir, string folderName)
    {
        var root = Path.Combine(projectDir, folderName);
        foreach (var lane in new[] { "Todo", "In Progress", "Testing", "Done", "Ideas", "Rejected" })
        {
            Directory.CreateDirectory(Path.Combine(root, lane));
        }

        return root;
    }

    private static async Task<T> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private sealed record BoardApiResponse(string Name, string Key);
    private sealed record MoveApiRequest(string Identifier, string Target);
    private sealed record WorkItemApiResponse(string Id, string Slug, string Status);
}
