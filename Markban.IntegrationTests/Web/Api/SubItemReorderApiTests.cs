using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class SubItemReorderApiTests : IDisposable
{
    private static readonly string[] DetachOrder = ["2a", "1", "2", "3"];

    private readonly ToolBuildFixture _build;
    private readonly List<string> _tempDirs = [];

    public SubItemReorderApiTests(ToolBuildFixture build)
    {
        _build = build;
    }

    [Theory]
    [InlineData(null, "warn")]
    [InlineData("block", "block")]
    [InlineData("force", "force")]
    public async Task GetSettings_ExposesSubItemReorderMode(string? configured, string expected)
    {
        // Arrange
        var board = CreateBoard("api-sub-settings", configured);
        await using var server = await WebTestServer.StartAsync(_build.DllPath, board);

        // Act
        var settings = await server.Client.GetFromJsonAsync<JsonElement>("/api/settings");

        // Assert
        settings.GetProperty("subItemReorder").GetString().Should().Be(expected);
    }

    [Fact]
    public async Task GetSettings_WithInvalidMode_Returns400WithClearError()
    {
        // Arrange
        var board = CreateBoard("api-sub-invalid", "sometimes");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, board);

        // Act
        using var response = await server.Client.GetAsync("/api/settings");
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("Invalid subItemReorder");
    }

    [Fact]
    public async Task PostReorder_BlockMode_RejectsSubItem_With400()
    {
        // Arrange
        var board = CreateBoard("api-sub-block", "block");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, board);

        // Act
        using var response = await PostDetachAsync(server, confirm: true);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("Sub-items stay with their parent");
        ReorderBoardBuilder.TodoFiles(board).Should().Contain("2a-beta-child.md");
    }

    [Fact]
    public async Task PostReorder_WarnMode_WithoutConfirm_Returns400()
    {
        // Arrange
        var board = CreateBoard("api-sub-warn", null);
        await using var server = await WebTestServer.StartAsync(_build.DllPath, board);

        // Act
        using var response = await PostDetachAsync(server, confirm: false);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ReorderBoardBuilder.TodoFiles(board).Should().Contain("2a-beta-child.md");
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("force", false)]
    public async Task PostReorder_DetachesSubItem_WhenConfirmedOrForced(string? mode, bool confirm)
    {
        // Arrange
        var board = CreateBoard("api-sub-detach", mode);
        await using var server = await WebTestServer.StartAsync(_build.DllPath, board);

        // Act
        using var response = await PostDetachAsync(server, confirm);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, because: await response.Content.ReadAsStringAsync());
        ReorderBoardBuilder.TodoFiles(board).Should().Equal(
            ["1-beta-child.md", "2-alpha.md", "3-beta.md", "4-gamma.md"]);
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs.Where(Directory.Exists))
        {
            Directory.Delete(dir, true);
        }
    }

    private static Task<HttpResponseMessage> PostDetachAsync(WebTestServer server, bool confirm)
    {
        return server.Client.PostAsJsonAsync(
            "/api/reorder",
            new { lane = "Todo", orderedIds = DetachOrder, detachId = "2a", confirmDetach = confirm });
    }

    private string CreateBoard(string prefix, string? mode)
        => ReorderBoardBuilder.Create(_tempDirs, prefix, mode, ReorderBoardBuilder.ParentAndSubItem);
}
