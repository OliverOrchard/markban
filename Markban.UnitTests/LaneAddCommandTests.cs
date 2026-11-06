using System.Text.Json;
using AwesomeAssertions;
using Xunit;

namespace Markban.UnitTests;

public class LaneAddCommandTests
{
    private const string LanesJson =
        "{\"rootPath\":\"./board\",\"lanes\":[" +
        "{\"name\":\"Todo\",\"ordered\":true,\"type\":\"ready\"}," +
        "{\"name\":\"Done\",\"ordered\":true,\"type\":\"done\"}]}";

    private static (string ProjectDir, string BoardDir) CreateBoard()
    {
        var projectDir = Path.Combine(Path.GetTempPath(), "lane-add-" + Guid.NewGuid().ToString("N")[..8]);
        var boardDir = Path.Combine(projectDir, "board");
        Directory.CreateDirectory(Path.Combine(boardDir, "Todo"));
        Directory.CreateDirectory(Path.Combine(boardDir, "Done"));
        File.WriteAllText(Path.Combine(projectDir, "markban.json"), LanesJson);
        return (projectDir, boardDir);
    }

    private static string ReadConfig(string projectDir) => File.ReadAllText(Path.Combine(projectDir, "markban.json"));

    private static JsonElement[] ReadLanes(string projectDir)
    {
        using var doc = JsonDocument.Parse(ReadConfig(projectDir));
        return doc.RootElement.GetProperty("lanes").EnumerateArray().Select(l => l.Clone()).ToArray();
    }

    [Fact]
    public void Execute_AppendsLaneWithDefaults_AndCreatesDirectory()
    {
        // Arrange
        var (projectDir, boardDir) = CreateBoard();

        // Act
        var result = LaneAddCommand.Execute(boardDir, "Review", new LaneAddOptions());

        // Assert
        result.Success.Should().BeTrue(because: result.Message);
        Directory.Exists(Path.Combine(boardDir, "Review")).Should().BeTrue();
        var lanes = ReadLanes(projectDir);
        lanes.Should().HaveCount(3);
        lanes[2].GetProperty("name").GetString().Should().Be("Review");
        lanes[2].GetProperty("ordered").GetBoolean().Should().BeTrue();
        lanes[2].TryGetProperty("type", out _).Should().BeFalse();
        lanes[2].TryGetProperty("pickable", out _).Should().BeFalse(because: "pickable defaults to true");
    }

    [Fact]
    public void Execute_AppliesFlagsToEntry()
    {
        // Arrange
        var (projectDir, boardDir) = CreateBoard();
        File.WriteAllText(Path.Combine(projectDir, "markban.json"), "{\"rootPath\":\"./board\",\"lanes\":[{\"name\":\"Todo\",\"ordered\":true}]}");

        // Act
        var result = LaneAddCommand.Execute(boardDir, "Shipped", new LaneAddOptions(Ordered: false, Type: "done", Pickable: false));

        // Assert
        result.Success.Should().BeTrue(because: result.Message);
        var lane = ReadLanes(projectDir)[1];
        lane.GetProperty("ordered").GetBoolean().Should().BeFalse();
        lane.GetProperty("type").GetString().Should().Be("done");
        lane.GetProperty("pickable").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void Execute_ReusesExistingDirectory_WhenNotInConfig()
    {
        // Arrange
        var (projectDir, boardDir) = CreateBoard();
        var dir = Path.Combine(boardDir, "Review");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "1-keep.md"), "# 1 - keep");

        // Act
        var result = LaneAddCommand.Execute(boardDir, "Review", new LaneAddOptions());

        // Assert
        result.Success.Should().BeTrue(because: result.Message);
        File.Exists(Path.Combine(dir, "1-keep.md")).Should().BeTrue();
        ReadLanes(projectDir).Should().HaveCount(3);
    }

    [Fact]
    public void Execute_FailsOnDuplicateName_AndChangesNothing()
    {
        // Arrange
        var (projectDir, boardDir) = CreateBoard();

        // Act
        var result = LaneAddCommand.Execute(boardDir, "todo", new LaneAddOptions());

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already exists");
        ReadConfig(projectDir).Should().Be(LanesJson);
    }

    [Theory]
    [InlineData("ready")]
    [InlineData("done")]
    public void Execute_FailsOnDuplicateType_AndChangesNothing(string type)
    {
        // Arrange
        var (projectDir, boardDir) = CreateBoard();

        // Act
        var result = LaneAddCommand.Execute(boardDir, "Extra", new LaneAddOptions(Type: type));

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain($"type '{type}'");
        ReadConfig(projectDir).Should().Be(LanesJson);
        Directory.Exists(Path.Combine(boardDir, "Extra")).Should().BeFalse();
    }

    [Fact]
    public void Execute_FailsOnInvalidTypeOrName()
    {
        // Arrange
        var (_, boardDir) = CreateBoard();

        // Act
        var badType = LaneAddCommand.Execute(boardDir, "Extra", new LaneAddOptions(Type: "bogus"));
        var badName = LaneAddCommand.Execute(boardDir, "../evil", new LaneAddOptions());

        // Assert
        badType.Success.Should().BeFalse();
        badName.Success.Should().BeFalse();
    }

    [Fact]
    public void Execute_DryRun_HasNoSideEffects()
    {
        // Arrange
        var (projectDir, boardDir) = CreateBoard();

        // Act
        var result = LaneAddCommand.Execute(boardDir, "Review", new LaneAddOptions(), dryRun: true);

        // Assert
        result.Success.Should().BeTrue(because: result.Message);
        result.DryRun.Should().BeTrue();
        result.Message.Should().Contain("Review");
        Directory.Exists(Path.Combine(boardDir, "Review")).Should().BeFalse();
        ReadConfig(projectDir).Should().Be(LanesJson);
    }
}
