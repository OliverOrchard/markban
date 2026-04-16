using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class MultiBoardOverviewTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly List<string> _tempDirs = [];

    public MultiBoardOverviewTests(ToolBuildFixture build)
    {
        _build = build;
    }

    [Fact]
    public async Task Overview_WithoutBoardsKey_RetainsSingleBoardOutput()
    {
        // Arrange
        var projectDir = CreateTempDir("overview-single");
        var boardRoot = CreateBoardRoot(projectDir, "board");
        File.WriteAllText(Path.Combine(projectDir, "markban.json"), """{"rootPath":"./board"}""");
        File.WriteAllText(Path.Combine(boardRoot, "Todo", "1-single-board.md"), "# 1 - Single Board\n\n## Description\n\nOnly board");

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, projectDir, "overview");

        // Assert
        result.ExitCode.Should().Be(0);
        result.StdOut.Should().Contain("1 todo");
        result.StdOut.Should().NotContain("=== ",
            because: "single-board mode should preserve the existing overview output");
    }

    [Fact]
    public async Task Overview_WithConfiguredBoards_PrintsEachBoardHeading()
    {
        // Arrange
        var projectDir = CreateTempDir("overview-multi");
        var mainRoot = CreateBoardRoot(projectDir, "main-board");
        var backendRoot = CreateBoardRoot(projectDir, "backend-board");
        var frontendRoot = CreateBoardRoot(projectDir, "frontend-board");
        File.WriteAllText(Path.Combine(backendRoot, "Todo", "1-backend-todo.md"), "# 1 - Backend Todo\n\n## Description\n\nBackend work");
        File.WriteAllText(Path.Combine(frontendRoot, "In Progress", "1-frontend-progress.md"), "# 1 - Frontend Progress\n\n## Description\n\nFrontend work");
        WriteMultiBoardConfig(projectDir, "main-board", [("Backend", "backend-board"), ("Frontend", "frontend-board")]);

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, projectDir, "overview");

        // Assert
        result.ExitCode.Should().Be(0);
        result.StdOut.Should().Contain("=== Backend ===");
        result.StdOut.Should().Contain("=== Frontend ===");
        result.StdOut.Should().Contain("Backend Todo");
        result.StdOut.Should().Contain("Frontend Progress");
    }

    [Fact]
    public async Task Overview_WithMissingBoardPath_WarnsAndContinues()
    {
        // Arrange
        var projectDir = CreateTempDir("overview-warning");
        var mainRoot = CreateBoardRoot(projectDir, "main-board");
        var backendRoot = CreateBoardRoot(projectDir, "backend-board");
        File.WriteAllText(Path.Combine(backendRoot, "Todo", "1-backend-task.md"), "# 1 - Backend Task\n\n## Description\n\nStill works");
        WriteMultiBoardConfig(projectDir, "main-board", [("Backend", "backend-board"), ("Missing", "missing-board")]);

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, projectDir, "overview");

        // Assert
        result.ExitCode.Should().Be(0,
            because: "overview is informational even when one configured board is unavailable");
        result.StdOut.Should().Contain("=== Backend ===");
        result.StdOut.Should().Contain("=== Missing ===");
        result.StdOut.Should().Contain("Warning: Board path not found:");
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

    private static void WriteMultiBoardConfig(string projectDir, string rootPath, IReadOnlyList<(string Name, string Path)> boards)
    {
        var boardsJson = string.Join(",", boards.Select(board => $"{{\"name\":\"{board.Name}\",\"path\":\"./{board.Path}\"}}"));
        File.WriteAllText(
            Path.Combine(projectDir, "markban.json"),
            $"{{\"rootPath\":\"./{rootPath}\",\"boards\":[{boardsJson}]}}");
    }
}
