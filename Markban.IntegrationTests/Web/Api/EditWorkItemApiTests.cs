using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class EditWorkItemApiTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly List<string> _tempDirs = [];

    public EditWorkItemApiTests(ToolBuildFixture build)
    {
        _build = build;
    }

    [Fact]
    public async Task PutItem_UpdatesFileOnDisk_AndNewContentReadableViaGet()
    {
        // Arrange
        var projectDir = CreateTempDir("api-edit-ok");
        var boardRoot = CreateBoardRoot(projectDir, "board");
        File.WriteAllText(Path.Combine(projectDir, "markban.json"), """{"rootPath":"./board"}""");
        File.WriteAllText(Path.Combine(boardRoot, "Todo", "1-editable-item.md"), "# 1 - Editable Item\n\n## Description\n\nOriginal.");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);

        // Act
        using var putResponse = await server.Client.PutAsJsonAsync(
            "/api/items/1",
            new { content = "# 1 - Editable Item\n\n## Description\n\nUpdated." });
        var putResult = await DeserializeAsync<JsonElement>(putResponse);
        using var getResponse = await server.Client.GetAsync("/api/items");
        var items = await DeserializeAsync<List<WorkItemApiResponse>>(getResponse);

        // Assert
        putResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            because: "a valid PUT request should succeed");
        putResult.GetProperty("message").GetString().Should().NotBeNullOrEmpty();
        var updatedItem = items.Should().ContainSingle(i => i.Slug == "editable-item").Subject;
        updatedItem.Content.Should().Contain("Updated.",
            because: "after a successful PUT the GET endpoint should reflect the new content");
        File.ReadAllText(Path.Combine(boardRoot, "Todo", "1-editable-item.md"))
            .Should().Contain("Updated.", because: "the updated content must be persisted to disk");
    }

    [Fact]
    public async Task PutItem_WithPathTraversalId_Returns400()
    {
        // Arrange
        var projectDir = CreateTempDir("api-edit-traversal");
        var boardRoot = CreateBoardRoot(projectDir, "board");
        File.WriteAllText(Path.Combine(projectDir, "markban.json"), """{"rootPath":"./board"}""");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);

        // Act — try to supply a path-traversal ID like ../secret or %2E%2E
        using var dotdotResponse = await server.Client.PutAsJsonAsync(
            "/api/items/..%2Fsecret",
            new { content = "pwned" });
        using var slashResponse = await server.Client.PutAsJsonAsync(
            "/api/items/1%2F..%2Fsecret",
            new { content = "pwned" });

        // Assert
        dotdotResponse.StatusCode.Should().BeOneOf(
            [HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.Forbidden],
            because: "a path-traversal attempt in the ID must be rejected");
        slashResponse.StatusCode.Should().BeOneOf(
            [HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.Forbidden],
            because: "a path-traversal attempt in the ID must be rejected");
    }

    [Fact]
    public async Task PutItem_WithUnknownBoard_Returns400()
    {
        // Arrange
        var projectDir = CreateTempDir("api-edit-badboard");
        var boardRoot = CreateBoardRoot(projectDir, "board");
        File.WriteAllText(Path.Combine(projectDir, "markban.json"), """{"rootPath":"./board"}""");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);

        // Act
        using var response = await server.Client.PutAsJsonAsync(
            "/api/items/1?board=does-not-exist",
            new { content = "irrelevant" });
        var result = await DeserializeAsync<JsonElement>(response);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.GetProperty("error").GetString().Should().Contain("Unknown board");
    }

    [Fact]
    public async Task PutItem_WithNonExistentId_Returns404()
    {
        // Arrange
        var projectDir = CreateTempDir("api-edit-404");
        var boardRoot = CreateBoardRoot(projectDir, "board");
        File.WriteAllText(Path.Combine(projectDir, "markban.json"), """{"rootPath":"./board"}""");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);

        // Act
        using var response = await server.Client.PutAsJsonAsync("/api/items/999", new { content = "nope" });

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            because: "a PUT for an item ID that does not exist should return 404");
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

    private sealed record WorkItemApiResponse(string Id, string Slug, string Status, string Content);
}
