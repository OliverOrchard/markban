using Markban.IntegrationTests.Infrastructure;
using AwesomeAssertions;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class InitTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly string _emptyDir;

    public InitTests(ToolBuildFixture build)
    {
        _build = build;
        _emptyDir = Path.Combine(Path.GetTempPath(), "wb-init-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_emptyDir);
    }

    [Fact]
    public async Task Init_CreatesStandardLanes_InWorkItems()
    {
        // Arrange — empty directory, no work-items/ yet

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, _emptyDir, "init");

        // Assert
        result.StdErr.Should().BeEmpty();
        Directory.Exists(Path.Combine(_emptyDir, "work-items", "Todo")).Should().BeTrue();
        Directory.Exists(Path.Combine(_emptyDir, "work-items", "In Progress")).Should().BeTrue();
        Directory.Exists(Path.Combine(_emptyDir, "work-items", "Testing")).Should().BeTrue();
        Directory.Exists(Path.Combine(_emptyDir, "work-items", "Done")).Should().BeTrue();
        Directory.Exists(Path.Combine(_emptyDir, "work-items", "Ideas")).Should().BeTrue();
        Directory.Exists(Path.Combine(_emptyDir, "work-items", "Rejected")).Should().BeTrue();
    }

    [Fact]
    public async Task Init_DoesNotWriteConfig_WhenDefaultPath()
    {
        // Arrange — empty directory, no markban.json

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, _emptyDir, "init");

        // Assert
        result.StdErr.Should().BeEmpty();
        File.Exists(Path.Combine(_emptyDir, "markban.json")).Should().BeFalse(
            because: "markban.json is only written when --path or --name is supplied");
    }

    [Fact]
    public async Task Init_WithPath_WritesMarkbanJson_AndStandardisedLanes()
    {
        // Arrange — empty directory

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, _emptyDir, "init", "--path", "my-tasks");

        // Assert
        result.StdErr.Should().BeEmpty();
        var configPath = Path.Combine(_emptyDir, "markban.json");
        File.Exists(configPath).Should().BeTrue();
        var content = File.ReadAllText(configPath);
        content.Should().Contain("\"rootPath\": \"./my-tasks\"");
        content.Should().Contain("\"type\": \"ready\"");
        content.Should().Contain("\"type\": \"done\"");
        content.Should().Contain("\"pickable\": false");
    }

    [Fact]
    public async Task Init_WithName_WritesNameInConfig()
    {
        // Arrange — empty directory

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, _emptyDir, "init", "--path", "tasks", "--name", "My Project");

        // Assert
        result.StdErr.Should().BeEmpty();
        var content = File.ReadAllText(Path.Combine(_emptyDir, "markban.json"));
        content.Should().Contain("\"name\": \"My Project\"");
    }

    [Fact]
    public async Task Init_DryRun_DoesNotCreateAnything()
    {
        // Arrange — empty directory

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, _emptyDir, "init", "--path", "tasks", "--dry-run");

        // Assert
        result.StdErr.Should().BeEmpty();
        result.StdOut.Should().Contain("DRY RUN");
        Directory.Exists(Path.Combine(_emptyDir, "tasks")).Should().BeFalse(
            because: "dry-run must not create directories");
        File.Exists(Path.Combine(_emptyDir, "markban.json")).Should().BeFalse(
            because: "dry-run must not create markban.json");
    }

    [Fact]
    public async Task Init_IsIdempotent_DoesNotDeleteExistingContent()
    {
        // Arrange — pre-existing board with a file in it
        var todoDir = Path.Combine(_emptyDir, "work-items", "Todo");
        Directory.CreateDirectory(todoDir);
        var existingFile = Path.Combine(todoDir, "1-existing.md");
        File.WriteAllText(existingFile, "# 1 - Existing");

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, _emptyDir, "init");

        // Assert
        result.StdErr.Should().BeEmpty();
        File.Exists(existingFile).Should().BeTrue(
            because: "existing files must not be deleted on re-run");
    }

    [Fact]
    public async Task Init_DoesNotOverwriteExistingMarkbanJson()
    {
        // Arrange — existing markban.json
        var configPath = Path.Combine(_emptyDir, "markban.json");
        const string original = """{"rootPath": "./existing"}""";
        File.WriteAllText(configPath, original);

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, _emptyDir, "init", "--path", "other");

        // Assert
        result.StdErr.Should().BeEmpty();
        result.StdOut.Should().Contain("Warning");
        File.ReadAllText(configPath).Should().Be(original,
            because: "existing markban.json must not be overwritten");
    }

    public void Dispose()
    {
        if (Directory.Exists(_emptyDir))
            Directory.Delete(_emptyDir, true);
    }
}
