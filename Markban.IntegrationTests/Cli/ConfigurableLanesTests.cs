using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Xunit;

namespace Markban.IntegrationTests;

/// <summary>
/// Integration tests for configurable lanes (item 5).
/// Verifies that the lanes array in markban.json drives CLI behaviour end-to-end.
/// </summary>
[Collection("CLI")]
public class ConfigurableLanesTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly List<string> _projectDirs = [];

    public ConfigurableLanesTests(ToolBuildFixture build)
    {
        _build = build;
    }

    /// <summary>
    /// Creates a self-contained project dir with <c>markban.json</c> and the configured lane subdirs.
    /// Returns (projectDir, boardDir).
    /// </summary>
    private (string ProjectDir, string BoardDir) CreateCustomBoard(
        string lanesJson,
        string boardSubDir = "board")
    {
        var projectDir = Path.Combine(Path.GetTempPath(), "mb-lanes-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(projectDir);
        _projectDirs.Add(projectDir);

        var boardDir = Path.Combine(projectDir, boardSubDir);
        Directory.CreateDirectory(boardDir);

        var config = $$"""{"rootPath": "./{{boardSubDir}}", "lanes": {{lanesJson}}}""";
        File.WriteAllText(Path.Combine(projectDir, "markban.json"), config);
        return (projectDir, boardDir);
    }

    private static void AddItem(string boardDir, string laneDir, string fileName, string content)
    {
        var dir = Path.Combine(boardDir, laneDir);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, fileName), content);
    }

    [Fact]
    public async Task List_ReturnsItemsFromCustomLanes()
    {
        // Arrange
        var lanesJson = "[{\"name\":\"Backlog\",\"ordered\":true,\"type\":\"ready\"},{\"name\":\"Doing\",\"ordered\":true},{\"name\":\"Shipped\",\"ordered\":true,\"type\":\"done\"}]";
        var (projectDir, boardDir) = CreateCustomBoard(lanesJson);
        AddItem(boardDir, "Backlog", "1-first-task.md", "# 1 - First Task\n\n## Description\n\nA backlog task.");
        AddItem(boardDir, "Doing", "2-in-flight.md", "# 2 - In Flight\n\n## Description\n\nActive work.");

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, projectDir, "list", "--summary");

        // Assert
        result.StdErr.Should().BeEmpty();
        var items = JsonHelper.DeserializeSummaries(result.StdOut);
        items.Should().HaveCount(2);
        items.Should().Contain(i => i.Slug == "first-task" && i.Status == "Backlog");
        items.Should().Contain(i => i.Slug == "in-flight" && i.Status == "Doing");
    }

    [Fact]
    public async Task Create_WithoutLane_UsesTypeReadyLane()
    {
        // Arrange
        var lanesJson = "[{\"name\":\"Backlog\",\"ordered\":true,\"type\":\"ready\"},{\"name\":\"Done\",\"ordered\":true,\"type\":\"done\"}]";
        var (projectDir, boardDir) = CreateCustomBoard(lanesJson);
        Directory.CreateDirectory(Path.Combine(boardDir, "Backlog"));
        Directory.CreateDirectory(Path.Combine(boardDir, "Done"));

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, projectDir, "create", "My Custom Task");

        // Assert
        result.StdErr.Should().BeEmpty();
        result.StdOut.Should().Contain("Backlog", because: "type:ready lane is Backlog");
        var files = Directory.GetFiles(Path.Combine(boardDir, "Backlog"), "*.md");
        files.Should().ContainSingle();
    }

    [Fact]
    public async Task Create_WithLaneFlag_ValidatesAgainstConfig()
    {
        // Arrange
        var lanesJson = "[{\"name\":\"Backlog\",\"ordered\":true,\"type\":\"ready\"},{\"name\":\"Done\",\"ordered\":true,\"type\":\"done\"}]";
        var (projectDir, boardDir) = CreateCustomBoard(lanesJson);
        Directory.CreateDirectory(Path.Combine(boardDir, "Backlog"));
        Directory.CreateDirectory(Path.Combine(boardDir, "Done"));

        // Act — pass hardcoded "Todo" which is not in this config
        var result = await CliRunner.RunInDirAsync(_build.DllPath, projectDir, "create", "Bad Lane Task", "--lane", "Todo");

        // Assert
        result.StdOut.Should().Contain("Error:", because: "Todo is not in the configured lanes");
    }

    [Fact]
    public async Task Move_ValidatesAgainstConfigLanes_NotHardcodedList()
    {
        // Arrange
        var lanesJson = "[{\"name\":\"Backlog\",\"ordered\":true,\"type\":\"ready\"},{\"name\":\"Doing\",\"ordered\":true},{\"name\":\"Done\",\"ordered\":true,\"type\":\"done\"}]";
        var (projectDir, boardDir) = CreateCustomBoard(lanesJson);
        AddItem(boardDir, "Backlog", "1-task.md", "# 1 - Task\n");

        // Act — "In Progress" is not configured
        var result = await CliRunner.RunInDirAsync(_build.DllPath, projectDir, "move", "task", "In Progress");

        // Assert
        result.StdOut.Should().Contain("Error:", because: "'In Progress' is not in this board's lane config");
    }

    [Fact]
    public async Task Move_OrderedToUnorderedLane_StripsNumberPrefix()
    {
        // Arrange
        var lanesJson = "[{\"name\":\"Active\",\"ordered\":true,\"type\":\"ready\"},{\"name\":\"Archive\",\"ordered\":false,\"pickable\":false},{\"name\":\"Done\",\"ordered\":true,\"type\":\"done\"}]";
        var (projectDir, boardDir) = CreateCustomBoard(lanesJson);
        AddItem(boardDir, "Active", "3-archive-me.md", "# 3 - Archive Me\n");
        Directory.CreateDirectory(Path.Combine(boardDir, "Archive"));
        Directory.CreateDirectory(Path.Combine(boardDir, "Done"));

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, projectDir, "move", "archive-me", "Archive");

        // Assert
        result.StdOut.Should().Contain("Successfully moved");
        var archiveFiles = Directory.GetFiles(Path.Combine(boardDir, "Archive"), "*.md")
                                    .Select(Path.GetFileName).ToArray();
        archiveFiles.Should().ContainSingle().Which.Should().Be("archive-me.md",
            because: "number prefix must be stripped when moving to an unordered lane");
    }

    [Fact]
    public async Task Reorder_FailsForUnorderedLane()
    {
        // Arrange
        var lanesJson = "[{\"name\":\"Active\",\"ordered\":true,\"type\":\"ready\"},{\"name\":\"Shelf\",\"ordered\":false,\"pickable\":false},{\"name\":\"Done\",\"ordered\":true,\"type\":\"done\"}]";
        var (projectDir, boardDir) = CreateCustomBoard(lanesJson);
        Directory.CreateDirectory(Path.Combine(boardDir, "Shelf"));

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, projectDir, "reorder", "Shelf", "2,1");

        // Assert
        result.StdOut.Should().Contain("Error:", because: "unordered lanes cannot be reordered");
    }

    [Fact]
    public async Task DefaultLanes_ApplyWhenNoLanesKeyInConfig()
    {
        // Arrange: board using standard lane folders but markban.json has no 'lanes' key
        var projectDir = Path.Combine(Path.GetTempPath(), "mb-default-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(projectDir);
        _projectDirs.Add(projectDir);

        var boardDir = Path.Combine(projectDir, "board");
        foreach (var lane in new[] { "Todo", "In Progress", "Testing", "Done", "Ideas", "Rejected" })
        {
            Directory.CreateDirectory(Path.Combine(boardDir, lane));
        }

        File.WriteAllText(Path.Combine(boardDir, "Todo", "1-default-item.md"), "# 1 - Default Item\n");
        File.WriteAllText(Path.Combine(projectDir, "markban.json"), """{"rootPath": "./board"}""");

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, projectDir, "list", "--summary");

        // Assert
        result.StdErr.Should().BeEmpty();
        var items = JsonHelper.DeserializeSummaries(result.StdOut);
        items.Should().ContainSingle().Which.Slug.Should().Be("default-item",
            because: "default lanes apply when 'lanes' key is absent");
    }

    [Fact]
    public async Task Create_AtWipLimit_RejectsWithError()
    {
        // Arrange: lane with wip:1 already containing one item
        var lanesJson = "[{\"name\":\"Active\",\"ordered\":true,\"type\":\"ready\",\"wip\":1},{\"name\":\"Done\",\"ordered\":true,\"type\":\"done\"}]";
        var (projectDir, boardDir) = CreateCustomBoard(lanesJson);
        AddItem(boardDir, "Active", "1-existing.md", "# 1 - Existing\n\n## Description\n\nAlready here");

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, projectDir, "create", "New Item", "--lane", "Active");

        // Assert
        result.StdOut.Should().Contain("WIP limit",
            because: "creating into a lane at its WIP limit should be rejected");
        result.StdOut.Should().Contain("1/1",
            because: "error should report the current and maximum counts");
        var files = Directory.GetFiles(Path.Combine(boardDir, "Active"), "*.md");
        files.Should().ContainSingle(because: "no second file should have been created");
    }

    [Fact]
    public async Task Create_AtWipLimit_WithOverrideWip_Succeeds()
    {
        // Arrange: lane with wip:1 already containing one item
        var lanesJson = "[{\"name\":\"Active\",\"ordered\":true,\"type\":\"ready\",\"wip\":1},{\"name\":\"Done\",\"ordered\":true,\"type\":\"done\"}]";
        var (projectDir, boardDir) = CreateCustomBoard(lanesJson);
        AddItem(boardDir, "Active", "1-existing.md", "# 1 - Existing\n\n## Description\n\nAlready here");

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, projectDir, "create", "Override Item", "--lane", "Active", "--override-wip");

        // Assert
        result.StdErr.Should().BeEmpty();
        result.StdOut.Should().Contain("Successfully created",
            because: "--override-wip should bypass the WIP limit");
        var files = Directory.GetFiles(Path.Combine(boardDir, "Active"), "*.md");
        files.Should().HaveCount(2, because: "second item should be created despite the WIP limit");
    }

    [Fact]
    public async Task Progress_SkipsNonPickableLanes()
    {
        // Arrange: workflow with a non-pickable Icebox lane between Active and Done
        var lanesJson = "[{\"name\":\"Active\",\"ordered\":true,\"type\":\"ready\"},{\"name\":\"Review\",\"ordered\":true},{\"name\":\"Icebox\",\"ordered\":false,\"pickable\":false},{\"name\":\"Done\",\"ordered\":true,\"type\":\"done\"}]";
        var (projectDir, boardDir) = CreateCustomBoard(lanesJson);
        foreach (var lane in new[] { "Active", "Review", "Icebox", "Done" })
        {
            Directory.CreateDirectory(Path.Combine(boardDir, lane));
        }

        AddItem(boardDir, "Review", "1-nearly-done.md", "# 1 - Nearly Done\n\n## Description\n\nAlmost there");

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, projectDir, "progress", "1");

        // Assert
        result.StdErr.Should().BeEmpty();
        result.StdOut.Should().Contain("Done", because: "progress should skip the non-pickable Icebox and land in Done");
        var doneFiles = Directory.GetFiles(Path.Combine(boardDir, "Done"), "*.md");
        doneFiles.Should().ContainSingle(because: "item should have moved to Done, bypassing Icebox");
        var iceboxFiles = Directory.GetFiles(Path.Combine(boardDir, "Icebox"), "*.md");
        iceboxFiles.Should().BeEmpty(because: "non-pickable Icebox should have been skipped");
    }

    public void Dispose()
    {
        foreach (var dir in _projectDirs)
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
