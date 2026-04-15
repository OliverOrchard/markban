using AwesomeAssertions;
using Xunit;

namespace Markban.UnitTests;

public class InitCommandTests
{
    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wb-init-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Execute_CreatesStandardLaneDirs_InWorkItems()
    {
        // Arrange
        var dir = CreateTempDir();

        try
        {
            // Act
            InitCommand.Execute(dir, boardPath: null, name: null, dryRun: false);

            // Assert
            var root = Path.Combine(dir, "work-items");
            Directory.Exists(Path.Combine(root, "Todo")).Should().BeTrue();
            Directory.Exists(Path.Combine(root, "In Progress")).Should().BeTrue();
            Directory.Exists(Path.Combine(root, "Testing")).Should().BeTrue();
            Directory.Exists(Path.Combine(root, "Done")).Should().BeTrue();
            Directory.Exists(Path.Combine(root, "Ideas")).Should().BeTrue();
            Directory.Exists(Path.Combine(root, "Rejected")).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Execute_WithCustomPath_CreatesLaneDirsUnderThatPath()
    {
        // Arrange
        var dir = CreateTempDir();

        try
        {
            // Act
            InitCommand.Execute(dir, boardPath: "my-tasks", name: null, dryRun: false);

            // Assert
            var root = Path.Combine(dir, "my-tasks");
            Directory.Exists(Path.Combine(root, "Todo")).Should().BeTrue();
            Directory.Exists(Path.Combine(root, "Done")).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Execute_WithCustomPath_WritesMarkbanJson()
    {
        // Arrange
        var dir = CreateTempDir();

        try
        {
            // Act
            InitCommand.Execute(dir, boardPath: "my-tasks", name: null, dryRun: false);

            // Assert
            var configPath = Path.Combine(dir, "markban.json");
            File.Exists(configPath).Should().BeTrue(because: "markban.json should be written when --path is used");
            var content = File.ReadAllText(configPath);
            content.Should().Contain("\"rootPath\": \"./my-tasks\"");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Execute_WithName_WritesNameFieldInConfig()
    {
        // Arrange
        var dir = CreateTempDir();

        try
        {
            // Act
            InitCommand.Execute(dir, boardPath: "tasks", name: "My Project", dryRun: false);

            // Assert
            var content = File.ReadAllText(Path.Combine(dir, "markban.json"));
            content.Should().Contain("\"name\": \"My Project\"");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Execute_IsIdempotent_DoesNotDeleteExistingDirs()
    {
        // Arrange
        var dir = CreateTempDir();
        var root = Path.Combine(dir, "work-items");
        Directory.CreateDirectory(Path.Combine(root, "Todo"));
        File.WriteAllText(Path.Combine(root, "Todo", "1-existing.md"), "# existing");

        try
        {
            // Act
            InitCommand.Execute(dir, boardPath: null, name: null, dryRun: false);

            // Assert
            File.Exists(Path.Combine(root, "Todo", "1-existing.md"))
                .Should().BeTrue(because: "existing files must not be deleted on re-run");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Execute_DoesNotOverwriteExistingMarkbanJson()
    {
        // Arrange
        var dir = CreateTempDir();
        var configPath = Path.Combine(dir, "markban.json");
        const string original = """{"rootPath": "./existing"}""";
        File.WriteAllText(configPath, original);

        try
        {
            // Act
            InitCommand.Execute(dir, boardPath: "other", name: null, dryRun: false);

            // Assert
            File.ReadAllText(configPath).Should().Be(original,
                because: "existing markban.json must not be overwritten");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Execute_DryRun_DoesNotCreateAnyFiles()
    {
        // Arrange
        var dir = CreateTempDir();

        try
        {
            // Act
            InitCommand.Execute(dir, boardPath: "tasks", name: "Test", dryRun: true);

            // Assert
            Directory.Exists(Path.Combine(dir, "tasks")).Should().BeFalse(
                because: "dry-run must not create directories");
            File.Exists(Path.Combine(dir, "markban.json")).Should().BeFalse(
                because: "dry-run must not create markban.json");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Execute_DefaultPath_DoesNotWriteMarkbanJson()
    {
        // Arrange
        var dir = CreateTempDir();

        try
        {
            // Act
            InitCommand.Execute(dir, boardPath: null, name: null, dryRun: false);

            // Assert
            File.Exists(Path.Combine(dir, "markban.json")).Should().BeFalse(
                because: "markban.json is only written when --path or --name is supplied");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
