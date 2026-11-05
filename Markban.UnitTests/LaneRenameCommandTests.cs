using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace Markban.UnitTests;

public class LaneRenameCommandTests
{
    private const string LanesJson =
        "{\"rootPath\":\"./board\",\"lanes\":[" +
        "{\"name\":\"Todo\",\"ordered\":true,\"type\":\"ready\"}," +
        "{\"name\":\"Done\",\"ordered\":true,\"type\":\"done\"}]}";

    private static (string ProjectDir, string BoardDir) CreateBoard()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), "lane-rename-" + Guid.NewGuid().ToString("N")[..8]);
        var boardDir = Path.Combine(projectDir, "board");
        Directory.CreateDirectory(Path.Combine(boardDir, "Todo"));
        Directory.CreateDirectory(Path.Combine(boardDir, "Done"));
        File.WriteAllText(Path.Combine(boardDir, "Todo", "1-item.md"), "# 1 - item");
        File.WriteAllText(Path.Combine(projectDir, "markban.json"), LanesJson);
        return (projectDir, boardDir);
    }

    private static List<string> ReadLaneNames(string projectDir)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(projectDir, "markban.json")));
        return doc.RootElement.GetProperty("lanes").EnumerateArray()
            .Select(l => l.GetProperty("name").GetString()!)
            .ToList();
    }

    [Fact]
    public void Execute_RenamesDirectoryAndConfig()
    {
        // Arrange
        var (projectDir, boardDir) = CreateBoard();

        // Act
        var result = LaneRenameCommand.Execute(boardDir, "Todo", "Backlog");

        // Assert
        result.Success.Should().BeTrue(because: result.Message);
        Directory.Exists(Path.Combine(boardDir, "Backlog")).Should().BeTrue();
        Directory.Exists(Path.Combine(boardDir, "Todo")).Should().BeFalse();
        File.Exists(Path.Combine(boardDir, "Backlog", "1-item.md")).Should().BeTrue();
        ReadLaneNames(projectDir).Should().Equal("Backlog", "Done");
    }

    [Fact]
    public void Execute_FailsWhenOldLaneMissing_AndChangesNothing()
    {
        // Arrange
        var (projectDir, boardDir) = CreateBoard();
        var before = File.ReadAllText(Path.Combine(projectDir, "markban.json"));

        // Act
        var result = LaneRenameCommand.Execute(boardDir, "Nope", "Backlog");

        // Assert
        result.Success.Should().BeFalse();
        Directory.Exists(Path.Combine(boardDir, "Backlog")).Should().BeFalse();
        File.ReadAllText(Path.Combine(projectDir, "markban.json")).Should().Be(before);
    }

    [Fact]
    public void Execute_FailsWhenTargetDirectoryExists_AndChangesNothing()
    {
        // Arrange
        var (projectDir, boardDir) = CreateBoard();
        Directory.CreateDirectory(Path.Combine(boardDir, "Backlog"));
        var before = File.ReadAllText(Path.Combine(projectDir, "markban.json"));

        // Act
        var result = LaneRenameCommand.Execute(boardDir, "Todo", "Backlog");

        // Assert
        result.Success.Should().BeFalse();
        Directory.Exists(Path.Combine(boardDir, "Todo")).Should().BeTrue();
        File.ReadAllText(Path.Combine(projectDir, "markban.json")).Should().Be(before);
    }

    [Fact]
    public void Execute_DryRun_HasNoSideEffects()
    {
        // Arrange
        var (projectDir, boardDir) = CreateBoard();
        var before = File.ReadAllText(Path.Combine(projectDir, "markban.json"));

        // Act
        var result = LaneRenameCommand.Execute(boardDir, "Todo", "Backlog", dryRun: true);

        // Assert
        result.Success.Should().BeTrue();
        result.DryRun.Should().BeTrue();
        Directory.Exists(Path.Combine(boardDir, "Todo")).Should().BeTrue();
        Directory.Exists(Path.Combine(boardDir, "Backlog")).Should().BeFalse();
        File.ReadAllText(Path.Combine(projectDir, "markban.json")).Should().Be(before);
    }

    [Fact]
    public void ExecuteCore_RollsBackDirectoryWhenConfigWriteFails()
    {
        // Arrange
        var (projectDir, boardDir) = CreateBoard();
        var before = File.ReadAllText(Path.Combine(projectDir, "markban.json"));

        // Act
        var result = LaneRenameCommand.ExecuteCore(
            boardDir, "Todo", "Backlog", false,
            Directory.Move,
            (_, _) => throw new IOException("disk full"));

        // Assert
        result.Success.Should().BeFalse();
        result.RolledBack.Should().BeTrue();
        Directory.Exists(Path.Combine(boardDir, "Todo")).Should().BeTrue();
        Directory.Exists(Path.Combine(boardDir, "Backlog")).Should().BeFalse();
        File.ReadAllText(Path.Combine(projectDir, "markban.json")).Should().Be(before);
    }

    [Fact]
    public void Execute_AllowsCaseOnlyRename()
    {
        // Arrange
        var (projectDir, boardDir) = CreateBoard();

        // Act
        var result = LaneRenameCommand.Execute(boardDir, "Todo", "todo");

        // Assert
        result.Success.Should().BeTrue(because: result.Message);
        Directory.GetDirectories(boardDir).Select(Path.GetFileName).Should().Contain("todo").And.NotContain("Todo");
        File.Exists(Path.Combine(boardDir, "todo", "1-item.md")).Should().BeTrue();
        ReadLaneNames(projectDir).Should().Equal("todo", "Done");
    }

    [Fact]
    public void Execute_FailsWithoutConfig_AndDoesNotCreateOne()
    {
        // Arrange
        var (projectDir, boardDir) = CreateBoard();
        File.Delete(Path.Combine(projectDir, "markban.json"));

        // Act
        var result = LaneRenameCommand.Execute(boardDir, "Todo", "Backlog");

        // Assert
        result.Success.Should().BeFalse();
        File.Exists(Path.Combine(projectDir, "markban.json")).Should().BeFalse();
        Directory.Exists(Path.Combine(boardDir, "Todo")).Should().BeTrue();
    }

    [Theory]
    [InlineData("..")]
    [InlineData("a/b")]
    [InlineData(" x")]
    public void Execute_RejectsUnsafeNewName(string newName)
    {
        // Arrange
        var (_, boardDir) = CreateBoard();

        // Act
        var result = LaneRenameCommand.Execute(boardDir, "Todo", newName);

        // Assert
        result.Success.Should().BeFalse();
        Directory.Exists(Path.Combine(boardDir, "Todo")).Should().BeTrue();
    }
}
