using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class BoardRemoveTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly string _projectDir = Path.Combine(Path.GetTempPath(), "board-remove-it-" + Guid.NewGuid().ToString("N")[..8]);

    public BoardRemoveTests(ToolBuildFixture build)
    {
        _build = build;
        Directory.CreateDirectory(Path.Combine(_projectDir, "board", "Todo"));
        Directory.CreateDirectory(Path.Combine(_projectDir, "other"));
        File.WriteAllText(Path.Combine(_projectDir, "markban.json"), """
            {
              "rootPath": "./board",
              "boards": [ { "name": "Other", "path": "other" } ]
            }
            """);
    }

    public void Dispose() => Directory.Delete(_projectDir, true);

    [Fact]
    public async Task BoardRemove_WithUnknownKey_ExitsNonZero()
    {
        // Arrange
        var before = File.ReadAllText(Path.Combine(_projectDir, "markban.json"));

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, _projectDir, "board", "remove", "missing");

        // Assert
        result.ExitCode.Should().Be(1, because: "an unknown key is a failure");
        result.StdErr.Should().Contain("No board with key 'missing'");
        File.ReadAllText(Path.Combine(_projectDir, "markban.json")).Should().Be(before);
    }

    [Fact]
    public async Task BoardRemove_WithInvalidConfig_ExitsNonZero()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_projectDir, "markban.json"), """{ "rootPath": "./board", "boards": [ """);

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, _projectDir, "board", "remove", "other");

        // Assert
        result.ExitCode.Should().Be(1, because: "an unparseable markban.json is a failure");
    }

    [Fact]
    public async Task BoardRemove_WithKnownKey_ExitsZeroAndRemovesBoardsKey()
    {
        // Arrange — config registers a single board named Other

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, _projectDir, "board", "remove", "other");

        // Assert
        result.ExitCode.Should().Be(0);
        File.ReadAllText(Path.Combine(_projectDir, "markban.json")).Should().NotContain("boards");
        Directory.Exists(Path.Combine(_projectDir, "other")).Should().BeTrue();
    }
}
