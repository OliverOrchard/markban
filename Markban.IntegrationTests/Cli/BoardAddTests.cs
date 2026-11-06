using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class BoardAddTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly string _projectDir = Path.Combine(Path.GetTempPath(), "board-add-it-" + Guid.NewGuid().ToString("N")[..8]);

    public BoardAddTests(ToolBuildFixture build)
    {
        _build = build;
        Directory.CreateDirectory(Path.Combine(_projectDir, "work-items", "Todo"));
        File.WriteAllText(Path.Combine(_projectDir, "markban.json"), """
            {
              "name": "Core",
              "rootPath": "./work-items"
            }
            """);
    }

    public void Dispose() => Directory.Delete(_projectDir, true);

    [Fact]
    public async Task BoardAdd_ResolvesPathRelativeToConfigDir_NotCwd()
    {
        // Arrange
        string subDir = Path.Combine(_projectDir, "src", "nested");
        Directory.CreateDirectory(subDir);

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, subDir, "board", "add", "Mobile", "services/mobile");

        // Assert
        result.ExitCode.Should().Be(0);
        Directory.Exists(Path.Combine(_projectDir, "services", "mobile", "work-items", "Todo")).Should().BeTrue(
            because: "<path> must be resolved relative to the root markban.json directory, not the CWD");
        string configText = File.ReadAllText(Path.Combine(_projectDir, "markban.json"));
        configText.Should().Contain("\"Core\"").And.Contain("\"Mobile\"");
    }

    [Fact]
    public async Task BoardAdd_WithDuplicateKey_ExitsNonZeroAndChangesNothing()
    {
        // Arrange
        await CliRunner.RunInDirAsync(_build.DllPath, _projectDir, "board", "add", "Mobile", "services/mobile");
        string before = File.ReadAllText(Path.Combine(_projectDir, "markban.json"));

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, _projectDir, "board", "add", "Mobile", "services/mobile-2");

        // Assert
        result.ExitCode.Should().Be(1, because: "a duplicate board key must exit non-zero");
        result.StdErr.Should().Contain("already exists");
        File.ReadAllText(Path.Combine(_projectDir, "markban.json")).Should().Be(before);
        Directory.Exists(Path.Combine(_projectDir, "services", "mobile-2")).Should().BeFalse();
    }

    [Fact]
    public async Task BoardAdd_WithDuplicatePath_ExitsNonZeroNamesExistingKeyAndChangesNothing()
    {
        // Arrange
        await CliRunner.RunInDirAsync(_build.DllPath, _projectDir, "board", "add", "Docs", "./docs");
        string before = File.ReadAllText(Path.Combine(_projectDir, "markban.json"));

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, _projectDir, "board", "add", "Docs2", "./docs");
        var dryRunResult = await CliRunner.RunInDirAsync(_build.DllPath, _projectDir, "board", "add", "Docs2", "./docs", "--dry-run");

        // Assert
        result.ExitCode.Should().Be(1, because: "registering a second board on the same path must exit non-zero");
        result.StdErr.Should().Contain("docs", because: "the error message must name the existing board key");
        dryRunResult.ExitCode.Should().Be(1, because: "--dry-run must report the same duplicate-path error");
        dryRunResult.StdErr.Should().Contain("docs");
        File.ReadAllText(Path.Combine(_projectDir, "markban.json")).Should().Be(before);
    }

    [Fact]
    public async Task BoardAdd_WithInvalidArgs_ExitsNonZero()
    {
        // Arrange — missing <path> argument

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, _projectDir, "board", "add", "Mobile");

        // Assert
        result.ExitCode.Should().Be(1, because: "missing required positional arguments is a failure");
    }

    [Fact]
    public async Task BoardAdd_WithDryRun_PrintsPreviewAndMakesNoChanges()
    {
        // Arrange
        string before = File.ReadAllText(Path.Combine(_projectDir, "markban.json"));

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, _projectDir, "board", "add", "Mobile", "services/mobile", "--dry-run");

        // Assert
        result.ExitCode.Should().Be(0);
        result.StdOut.Should().Contain("Would bootstrap board 'Mobile'");
        result.StdOut.Should().Contain("Would add board 'Mobile' (mobile)");
        File.ReadAllText(Path.Combine(_projectDir, "markban.json")).Should().Be(before);
        Directory.Exists(Path.Combine(_projectDir, "services", "mobile")).Should().BeFalse();
    }
}
