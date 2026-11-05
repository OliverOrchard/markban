using AwesomeAssertions;
using Xunit;

public class BoardRemoveCommandTests : IDisposable
{
    private const string TwoBoardConfig = """
        {
          "rootPath": "./work-items",
          "boards": [
            { "name": "Backend", "path": "services/api" },
            { "name": "Frontend", "path": "services/web" }
          ]
        }
        """;

    private readonly string _projectDir = Path.Combine(Path.GetTempPath(), "board-remove-" + Guid.NewGuid().ToString("N"));

    private string RootPath => Path.Combine(_projectDir, "work-items");

    private string ConfigPath => Path.Combine(_projectDir, "markban.json");

    public BoardRemoveCommandTests()
    {
        Directory.CreateDirectory(RootPath);
        Directory.CreateDirectory(Path.Combine(_projectDir, "services", "api"));
        Directory.CreateDirectory(Path.Combine(_projectDir, "services", "web"));
    }

    public void Dispose() => Directory.Delete(_projectDir, true);

    [Fact]
    public void Execute_RemovesEntry_AndLeavesDirectoryUntouched()
    {
        // Arrange
        File.WriteAllText(ConfigPath, TwoBoardConfig);
        var boardDir = Path.Combine(_projectDir, "services", "api");
        File.WriteAllText(Path.Combine(boardDir, "keep.md"), "content");

        // Act
        var result = BoardRemoveCommand.Execute(_projectDir, RootPath, "backend", dryRun: false);

        // Assert
        result.Status.Should().Be(BoardRemoveStatus.Removed);
        var boards = WorkItemStore.LoadBoards(_projectDir);
        boards.Should().ContainSingle().Which.Key.Should().Be("frontend");
        File.ReadAllText(Path.Combine(boardDir, "keep.md")).Should().Be("content");
        File.ReadAllText(ConfigPath).Should().Contain("rootPath", because: "other config keys are preserved");
    }

    [Fact]
    public void Execute_WhenEntryIsRootBoard_ReturnsErrorAndChangesNothing()
    {
        // Arrange
        File.WriteAllText(ConfigPath, """
            {
              "rootPath": "./work-items",
              "boards": [
                { "name": "Main", "path": "work-items" },
                { "name": "Other", "path": "services/web" }
              ]
            }
            """);
        var before = File.ReadAllText(ConfigPath);

        // Act
        var result = BoardRemoveCommand.Execute(_projectDir, RootPath, "main", dryRun: false);

        // Assert
        result.Status.Should().Be(BoardRemoveStatus.RootBoard);
        File.ReadAllText(ConfigPath).Should().Be(before);
    }

    [Fact]
    public void Execute_WhenKeyNotFound_ReturnsErrorAndChangesNothing()
    {
        // Arrange
        File.WriteAllText(ConfigPath, TwoBoardConfig);
        var before = File.ReadAllText(ConfigPath);

        // Act
        var result = BoardRemoveCommand.Execute(_projectDir, RootPath, "missing", dryRun: false);

        // Assert
        result.Status.Should().Be(BoardRemoveStatus.NotFound);
        File.ReadAllText(ConfigPath).Should().Be(before);
    }

    [Fact]
    public void Execute_WhenLastEntryRemoved_RemovesBoardsKey()
    {
        // Arrange
        File.WriteAllText(ConfigPath, """
            {
              "rootPath": "./work-items",
              "boards": [ { "name": "Backend", "path": "services/api" } ]
            }
            """);

        // Act
        var result = BoardRemoveCommand.Execute(_projectDir, RootPath, "backend", dryRun: false);

        // Assert
        result.BoardsKeyRemoved.Should().BeTrue();
        File.ReadAllText(ConfigPath).Should().NotContain("boards");
        File.ReadAllText(ConfigPath).Should().Contain("rootPath");
    }

    [Fact]
    public void Execute_WithDryRun_DoesNotModifyConfigOrDirectory()
    {
        // Arrange
        File.WriteAllText(ConfigPath, TwoBoardConfig);
        var before = File.ReadAllText(ConfigPath);

        // Act
        var result = BoardRemoveCommand.Execute(_projectDir, RootPath, "backend", dryRun: true);

        // Assert
        result.Status.Should().Be(BoardRemoveStatus.DryRun);
        result.BoardName.Should().Be("Backend");
        File.ReadAllText(ConfigPath).Should().Be(before);
        Directory.Exists(Path.Combine(_projectDir, "services", "api")).Should().BeTrue();
    }
}
