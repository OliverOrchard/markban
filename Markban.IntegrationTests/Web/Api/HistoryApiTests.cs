using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class HistoryApiTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly List<string> _tempDirs = [];

    public HistoryApiTests(ToolBuildFixture build)
    {
        _build = build;
    }

    public void Dispose()
    {
        foreach (string dir in _tempDirs.Where(Directory.Exists))
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task CliReorder_CanBeUndoneAndRedone_ThroughWebApi()
    {
        // Arrange
        string board = ReorderBoardBuilder.Create(
            _tempDirs,
            "api-cli-undo",
            subItemReorder: null,
            ("1-alpha.md", "alpha"),
            ("2-beta.md", "beta"),
            ("3-gamma.md", "gamma"));
        var cliResult = await CliRunner.RunAsync(_build.DllPath, board, "reorder", "Todo", "3,1,2");
        cliResult.ExitCode.Should().Be(0, because: cliResult.StdErr);
        await using var server = await WebTestServer.StartAsync(_build.DllPath, board);

        // Act
        using var historyBefore = await server.Client.GetAsync("/api/history");
        var historyDoc = JsonDocument.Parse(await historyBefore.Content.ReadAsStringAsync());
        using var undoResponse = await server.Client.PostAsync("/api/undo", content: null);
        string[] afterUndo = ReorderBoardBuilder.TodoFiles(board);
        using var redoResponse = await server.Client.PostAsync("/api/redo", content: null);
        string[] afterRedo = ReorderBoardBuilder.TodoFiles(board);

        // Assert
        historyBefore.StatusCode.Should().Be(HttpStatusCode.OK);
        historyDoc.RootElement.GetProperty("canUndo").GetBoolean().Should().BeTrue();
        historyDoc.RootElement.GetProperty("undo").GetArrayLength().Should().Be(1);
        undoResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        afterUndo.Should().Equal("1-alpha.md", "2-beta.md", "3-gamma.md");
        redoResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        afterRedo.Should().Equal("1-gamma.md", "2-alpha.md", "3-beta.md");
    }

    [Fact]
    public async Task WebReorder_CanBeUndoneByCli_AndRefusedWhenModified()
    {
        // Arrange
        string board = ReorderBoardBuilder.Create(
            _tempDirs,
            "api-web-undo",
            subItemReorder: null,
            ("1-alpha.md", "alpha"),
            ("2-beta.md", "beta"));
        await using var server = await WebTestServer.StartAsync(_build.DllPath, board);
        using var reorderResponse = await server.Client.PostAsJsonAsync(
            "/api/reorder",
            new { lane = "Todo", orderedIds = new[] { "2", "1" } });
        reorderResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        var cliUndo = await CliRunner.RunAsync(_build.DllPath, board, "undo");
        string[] afterCliUndo = ReorderBoardBuilder.TodoFiles(board);
        File.AppendAllText(Path.Combine(board, "Todo", "1-alpha.md"), "\nManual edit\n");
        using var badRedoResponse = await server.Client.PostAsync("/api/redo", content: null);
        string badRedoBody = await badRedoResponse.Content.ReadAsStringAsync();

        // Assert
        cliUndo.ExitCode.Should().Be(0, because: cliUndo.StdErr);
        afterCliUndo.Should().Equal("1-alpha.md", "2-beta.md");
        badRedoResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        badRedoBody.Should().Contain("Todo/1-alpha.md");
    }
}
