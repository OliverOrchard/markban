using System.Text.Json;
using AwesomeAssertions;
using Xunit;

public class BoardAddCommandTests : IDisposable
{
    private const string SingleBoardConfig = """
        {
          "rootPath": "./work-items"
        }
        """;

    private readonly string _projectDir = Path.Combine(Path.GetTempPath(), "board-add-" + Guid.NewGuid().ToString("N"));

    private string RootPath => Path.Combine(_projectDir, "work-items");

    private string ConfigPath => Path.Combine(_projectDir, "markban.json");

    public BoardAddCommandTests()
    {
        Directory.CreateDirectory(RootPath);
    }

    public void Dispose() => Directory.Delete(_projectDir, true);

    [Fact]
    public void Execute_NewBoardDirectory_CreatesDirectoryAndConfig_AndAppendsToRootConfig()
    {
        // Arrange
        File.WriteAllText(ConfigPath, SingleBoardConfig);
        string mobileDir = Path.Combine(_projectDir, "services", "mobile");

        // Act
        var result = BoardAddCommand.Execute(_projectDir, RootPath, "Mobile", "services/mobile");

        // Assert
        result.Status.Should().Be(BoardAddStatus.Added);
        result.Bootstrapped.Should().BeTrue();
        result.Linked.Should().BeTrue();
        File.Exists(Path.Combine(mobileDir, "markban.json")).Should().BeTrue();
        Directory.Exists(Path.Combine(mobileDir, "work-items", "Todo")).Should().BeTrue();
        Directory.Exists(Path.Combine(mobileDir, "work-items", "Done")).Should().BeTrue();
        var boards = WorkItemStore.LoadBoards(_projectDir);
        boards.Select(b => b.Key).Should().Contain("mobile");
    }

    [Fact]
    public void Execute_ExistingValidBoardDirectory_ReusesWithoutModification()
    {
        // Arrange
        File.WriteAllText(ConfigPath, SingleBoardConfig);
        string existingDir = Path.Combine(_projectDir, "existing-board");
        Directory.CreateDirectory(Path.Combine(existingDir, "Todo"));
        File.WriteAllText(Path.Combine(existingDir, "Todo", "1-keep.md"), "# 1 - Keep");

        // Act
        var result = BoardAddCommand.Execute(_projectDir, RootPath, "Existing", "existing-board");

        // Assert
        result.Status.Should().Be(BoardAddStatus.Added);
        result.Bootstrapped.Should().BeFalse(because: "existing valid board directory must not be modified");
        File.Exists(Path.Combine(existingDir, "markban.json")).Should().BeFalse();
        Directory.Exists(Path.Combine(existingDir, "work-items")).Should().BeFalse();
        File.ReadAllText(Path.Combine(existingDir, "Todo", "1-keep.md")).Should().Be("# 1 - Keep");
        WorkItemStore.LoadBoards(_projectDir).Select(b => b.Key).Should().Contain("existing");
    }

    [Fact]
    public void Execute_WithNoLink_BootstrapsBoardWithoutModifyingRootConfig()
    {
        // Arrange
        File.WriteAllText(ConfigPath, SingleBoardConfig);
        string before = File.ReadAllText(ConfigPath);
        string standaloneDir = Path.Combine(_projectDir, "standalone");

        // Act
        var result = BoardAddCommand.Execute(_projectDir, RootPath, "Standalone", "standalone", noLink: true);

        // Assert
        result.Status.Should().Be(BoardAddStatus.Added);
        result.Bootstrapped.Should().BeTrue();
        result.Linked.Should().BeFalse();
        File.Exists(Path.Combine(standaloneDir, "markban.json")).Should().BeTrue();
        Directory.Exists(Path.Combine(standaloneDir, "work-items", "Todo")).Should().BeTrue();
        File.ReadAllText(ConfigPath).Should().Be(before, because: "--no-link must not modify the current root config");
    }

    [Fact]
    public void Execute_WithCopyConfig_CopiesOnlyBoardLocalSettings()
    {
        // Arrange
        File.WriteAllText(ConfigPath, """
            {
              "name": "Main",
              "rootPath": "./custom-root",
              "boards": [
                { "name": "Main", "path": "./custom-root" }
              ],
              "lanes": [
                { "name": "Backlog", "ordered": true, "type": "ready", "wip": 3 },
                { "name": "Shipped", "ordered": true, "type": "done" }
              ],
              "wipLimits": { "Backlog": 3 },
              "commitTags": ["feat", "fix"],
              "commitMessageMaxLength": 50,
              "slugCasing": "snake",
              "h1Heading": false,
              "commit": { "maxMessageLength": 50, "tags": ["feat", "fix"] },
              "slugs": { "casing": "snake" },
              "heading": { "enabled": false }
            }
            """);
        string targetDir = Path.Combine(_projectDir, "cloned");

        // Act
        var result = BoardAddCommand.Execute(_projectDir, RootPath, "Cloned", "cloned", copyConfig: true);

        // Assert
        result.Status.Should().Be(BoardAddStatus.Added);
        string clonedConfigText = File.ReadAllText(Path.Combine(targetDir, "markban.json"));
        using var doc = JsonDocument.Parse(clonedConfigText);
        doc.RootElement.GetProperty("rootPath").GetString().Should().Be("./work-items",
            because: "rootPath from the source board must not be copied");
        doc.RootElement.TryGetProperty("boards", out _).Should().BeFalse(
            because: "boards array from the source board must not be copied");
        doc.RootElement.GetProperty("name").GetString().Should().Be("Cloned");
        doc.RootElement.GetProperty("wipLimits").GetProperty("Backlog").GetInt32().Should().Be(3);
        doc.RootElement.GetProperty("commitMessageMaxLength").GetInt32().Should().Be(50);
        doc.RootElement.GetProperty("slugCasing").GetString().Should().Be("snake");
        doc.RootElement.GetProperty("h1Heading").GetBoolean().Should().BeFalse();

        string clonedWorkItems = Path.Combine(targetDir, "work-items");
        var lanes = WorkItemStore.LoadConfig(clonedWorkItems);
        lanes.Select(l => l.Name).Should().Equal(["Backlog", "Shipped"]);
        lanes[0].Wip.Should().Be(3);
        var settings = WorkItemStore.LoadSettings(clonedWorkItems);
        settings.CommitMaxMessageLength.Should().Be(50);
        settings.CommitTags.Should().Equal(["feat", "fix"]);
        settings.SlugCasing.Should().Be("snake");
        settings.HeadingEnabled.Should().BeFalse();
    }

    [Fact]
    public void Execute_OnFirstWriteOfBoardsArray_IncludesRootBoardAsFirstEntry()
    {
        // Arrange
        File.WriteAllText(ConfigPath, """
            {
              "name": "Platform",
              "rootPath": "./work-items"
            }
            """);

        // Act
        var result = BoardAddCommand.Execute(_projectDir, RootPath, "Mobile", "mobile");

        // Assert
        result.Status.Should().Be(BoardAddStatus.Added);
        result.RootBoardAdded.Should().BeTrue();
        using var doc = JsonDocument.Parse(File.ReadAllText(ConfigPath));
        var boardsArray = doc.RootElement.GetProperty("boards");
        boardsArray.GetArrayLength().Should().Be(2);
        boardsArray[0].GetProperty("name").GetString().Should().Be("Platform");
        boardsArray[0].GetProperty("path").GetString().Should().Be("./work-items");
        boardsArray[1].GetProperty("name").GetString().Should().Be("Mobile");
        boardsArray[1].GetProperty("path").GetString().Should().Be("mobile");
    }

    [Fact]
    public void Execute_WhenBoardKeyAlreadyExists_ReturnsErrorAndChangesNothing()
    {
        // Arrange
        File.WriteAllText(ConfigPath, """
            {
              "rootPath": "./work-items",
              "boards": [
                { "name": "Work-items", "path": "./work-items" },
                { "name": "Mobile", "path": "./mobile" }
              ]
            }
            """);
        string before = File.ReadAllText(ConfigPath);

        // Act
        var result = BoardAddCommand.Execute(_projectDir, RootPath, "Mobile", "other-mobile");

        // Assert
        result.Status.Should().Be(BoardAddStatus.DuplicateKey);
        File.ReadAllText(ConfigPath).Should().Be(before);
        Directory.Exists(Path.Combine(_projectDir, "other-mobile")).Should().BeFalse();
    }

    [Fact]
    public void Execute_WhenBoardPathAlreadyRegistered_ReturnsErrorNamingExistingKeyAndChangesNothing()
    {
        // Arrange
        BoardAddCommand.Execute(_projectDir, Path.Combine(_projectDir, "work-items"), "Docs", "./docs");
        File.WriteAllText(ConfigPath, """
            {
              "name": "Main",
              "rootPath": "./work-items",
              "boards": [
                { "name": "Main", "path": "./work-items" },
                { "name": "Docs", "path": "./docs" }
              ]
            }
            """);
        string before = File.ReadAllText(ConfigPath);

        // Act
        var duplicateBoardResult = BoardAddCommand.Execute(_projectDir, RootPath, "Docs2", "./docs/");
        var rootPathResult = BoardAddCommand.Execute(_projectDir, RootPath, "RootAlias", "./work-items", dryRun: true);
        var noLinkResult = BoardAddCommand.Execute(_projectDir, RootPath, "Docs2", "./docs", noLink: true);

        // Assert
        duplicateBoardResult.Status.Should().Be(BoardAddStatus.DuplicatePath);
        duplicateBoardResult.ExistingKey.Should().Be("docs");
        duplicateBoardResult.ErrorMessage.Should().Contain("docs");
        rootPathResult.Status.Should().Be(BoardAddStatus.DuplicatePath);
        rootPathResult.ExistingKey.Should().Be("main");
        noLinkResult.Status.Should().Be(BoardAddStatus.Added, because: "--no-link must not apply the duplicate-path link check");
        File.ReadAllText(ConfigPath).Should().Be(before);
    }

    [Fact]
    public void Execute_WithDryRun_MakesNoChanges()
    {
        // Arrange
        File.WriteAllText(ConfigPath, SingleBoardConfig);
        string before = File.ReadAllText(ConfigPath);

        // Act
        var result = BoardAddCommand.Execute(_projectDir, RootPath, "Mobile", "mobile", dryRun: true);

        // Assert
        result.Status.Should().Be(BoardAddStatus.DryRun);
        result.Bootstrapped.Should().BeTrue();
        result.Linked.Should().BeTrue();
        File.ReadAllText(ConfigPath).Should().Be(before);
        Directory.Exists(Path.Combine(_projectDir, "mobile")).Should().BeFalse();
    }
}
