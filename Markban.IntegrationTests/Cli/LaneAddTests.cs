using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class LaneAddTests : IDisposable
{
    private const string Config =
        "{\"rootPath\":\"./board\",\"lanes\":[{\"name\":\"Todo\",\"ordered\":true,\"type\":\"ready\"},{\"name\":\"Done\",\"ordered\":true,\"type\":\"done\"}]}";

    private readonly ToolBuildFixture _build;
    private readonly string _projectDir;

    public LaneAddTests(ToolBuildFixture build)
    {
        _build = build;
        _projectDir = Path.Combine(Path.GetTempPath(), "mb-laneadd-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_projectDir, "board", "Todo"));
        Directory.CreateDirectory(Path.Combine(_projectDir, "board", "Done"));
        File.WriteAllText(Path.Combine(_projectDir, "markban.json"), Config);
    }

    [Fact]
    public async Task LaneAdd_CreatesLane_AndExitsZero()
    {
        // Arrange — board from constructor

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, _projectDir, "lane", "add", "Review", "--unordered");

        // Assert
        result.ExitCode.Should().Be(0, because: result.StdErr);
        Directory.Exists(Path.Combine(_projectDir, "board", "Review")).Should().BeTrue();
        File.ReadAllText(Path.Combine(_projectDir, "markban.json")).Should().Contain("Review");
    }

    [Theory]
    [InlineData("Todo")]
    [InlineData("Extra --type ready")]
    [InlineData("Extra --type bogus")]
    [InlineData("Extra --bogus")]
    [InlineData("Extra --type")]
    [InlineData("")]
    public async Task LaneAdd_FailurePaths_ExitNonZero(string argString)
    {
        // Arrange
        var args = new[] { "lane", "add" }.Concat(argString.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToArray();

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, _projectDir, args);

        // Assert
        result.ExitCode.Should().NotBe(0);
        File.ReadAllText(Path.Combine(_projectDir, "markban.json")).Should().Be(Config);
    }

    public void Dispose()
    {
        if (Directory.Exists(_projectDir))
        {
            Directory.Delete(_projectDir, recursive: true);
        }
    }
}
