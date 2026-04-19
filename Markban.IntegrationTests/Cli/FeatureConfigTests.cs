using System.Text;
using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Xunit;

namespace Markban.IntegrationTests;

/// <summary>
/// Tests that require a markban.json with feature flags.
/// LoadSettings() reads config from Path.GetDirectoryName(rootPath), so the workspace
/// must live in a subdirectory of the directory that contains markban.json.
/// </summary>
[Collection("CLI")]
public class FeatureConfigTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly List<string> _tempDirs = [];

    public FeatureConfigTests(ToolBuildFixture build)
    {
        _build = build;
    }

    // -------------------------------------------------------------------------
    // Help — disabled-feature commands are hidden
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Help_DoesNotShowBlockCommand_WhenBlockedFeatureDisabled()
    {
        // Arrange
        var wsRoot = CreateBoard("""
            "blocked": { "enabled": false }
            """);
        AddItem(wsRoot, "Todo", "1-task.md", "# 1 - Task\n\n## Description\n\nContent");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, wsRoot, "--help");

        // Assert
        result.StdOut.Should().NotContain("block <id|slug>",
            because: "the 'block' command entry should be hidden from help when blocked feature is disabled");
    }

    [Fact]
    public async Task Help_DoesNotShowTagCommand_WhenTagsFeatureDisabled()
    {
        // Arrange
        var wsRoot = CreateBoard("""
            "tags": { "enabled": false }
            """);
        AddItem(wsRoot, "Todo", "1-task.md", "# 1 - Task\n\n## Description\n\nContent");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, wsRoot, "--help");

        // Assert
        result.StdOut.Should().NotContain("  tag ",
            because: "the 'tag' command should be hidden from help when tags feature is disabled");
    }

    [Fact]
    public async Task Help_DoesNotShowDependsOnCommand_WhenDependsOnFeatureDisabled()
    {
        // Arrange
        var wsRoot = CreateBoard("""
            "dependsOn": { "enabled": false }
            """);
        AddItem(wsRoot, "Todo", "1-task.md", "# 1 - Task\n\n## Description\n\nContent");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, wsRoot, "--help");

        // Assert
        result.StdOut.Should().NotContain("depends-on",
            because: "the 'depends-on' command should be hidden from help when dependsOn feature is disabled");
    }

    // -------------------------------------------------------------------------
    // Help — feature-gated flags on core commands are hidden
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Help_DoesNotShowFilterTagFlag_WhenTagsFeatureDisabled()
    {
        // Arrange
        var wsRoot = CreateBoard("""
            "tags": { "enabled": false }
            """);

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, wsRoot, "--help");

        // Assert
        result.StdOut.Should().NotContain("--filter-tag",
            because: "the --filter-tag flag on 'list' should be hidden when tags is disabled");
    }

    [Fact]
    public async Task Help_DoesNotShowTagsFlag_OnCreate_WhenTagsFeatureDisabled()
    {
        // Arrange
        var wsRoot = CreateBoard("""
            "tags": { "enabled": false }
            """);

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, wsRoot, "--help");

        // Assert
        result.StdOut.Should().NotContain("--tags",
            because: "the --tags flag on 'create' should be hidden when tags is disabled");
    }

    [Fact]
    public async Task Help_DoesNotShowIncludeBlockedFlag_WhenBlockedFeatureDisabled()
    {
        // Arrange
        var wsRoot = CreateBoard("""
            "blocked": { "enabled": false }
            """);

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, wsRoot, "--help");

        // Assert
        result.StdOut.Should().NotContain("--include-blocked",
            because: "the --include-blocked flag on 'next' should be hidden when blocked is disabled");
    }

    // -------------------------------------------------------------------------
    // create --set — reserved name aborts item creation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_WithReservedSetKey_ReturnsError_AndDoesNotCreateItem()
    {
        // Arrange
        var wsRoot = CreateBoard();

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, wsRoot,
            "create", "Reserved Test", "--set", "customFrontmatter=bad");

        // Assert
        result.StdErr.Should().Contain("reserved",
            because: "using a reserved --set key should print a clear error");
        var files = Directory.GetFiles(Path.Combine(wsRoot, "Todo"), "*.md");
        files.Should().BeEmpty(
            because: "the item must not be created when a reserved --set key is used");
    }



    [Fact]
    public async Task Next_WhenAllTodoItemsAreBlocked_StdOutIsEmpty()
    {
        // Arrange
        var wsRoot = CreateBoard();
        AddItem(wsRoot, "Todo", "1-blocked-task.md",
            "---\nblocked: waiting on design\n---\n\n# 1 - Blocked Task\n\n## Description\n\nBlocked");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, wsRoot, "next");

        // Assert
        result.StdOut.Should().BeEmpty(
            because: "when no actionable items exist, stdout should be empty so machine consumers get a clean empty signal");
        result.StdErr.Should().Contain("No actionable items",
            because: "a human-readable message should go to stderr");
    }

    [Fact]
    public async Task Next_WhenReadyLaneHasUnblockedItem_OutputsItem()
    {
        // Arrange
        var wsRoot = CreateBoard();
        AddItem(wsRoot, "Todo", "1-blocked.md",
            "---\nblocked: waiting\n---\n\n# 1 - Blocked\n\n## Description\n\nBlocked");
        AddItem(wsRoot, "Todo", "2-ready.md",
            "# 2 - Ready\n\n## Description\n\nNot blocked");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, wsRoot, "next");

        // Assert
        result.StdOut.Should().Contain("\"Slug\": \"ready\"",
            because: "the unblocked item (slug=ready) should be returned by next");
        result.StdOut.Should().NotContain("\"Slug\": \"blocked\"",
            because: "the blocked item should be skipped");
    }

    // -------------------------------------------------------------------------
    // customFrontmatter — null defaults written as YAML null
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_WithNullCustomDefault_WritesYamlNullToFrontmatter()
    {
        // Arrange
        var wsRoot = CreateBoard("""
            "customFrontmatter": [
                { "name": "estimate", "default": null },
                { "name": "priority", "default": "medium" }
            ]
            """);

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, wsRoot, "create", "Null Default Test");

        // Assert
        result.StdOut.Should().Contain("Successfully created",
            because: "create should succeed even with null defaults");

        var files = Directory.GetFiles(Path.Combine(wsRoot, "Todo"), "*.md");
        files.Should().ContainSingle(because: "exactly one file should be created");

        var content = File.ReadAllText(files[0]);
        content.Should().Contain("estimate: null",
            because: "a null default in config should write YAML null to the frontmatter field");
        content.Should().Contain("priority: medium",
            because: "a non-null default in config should write the value to the frontmatter field");
    }

    // -------------------------------------------------------------------------
    // health check-order --fix
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CheckOrderFix_ReordersItemsWhenDependencyHasHigherIdInSameLane()
    {
        // Arrange — item alpha (id=1) depends on item beta (id=2): violation, beta should come first
        var wsRoot = CreateBoard();
        AddItem(wsRoot, "Todo", "1-alpha.md",
            "---\ndependsOn: [beta]\n---\n\n# 1 - Alpha\n\n## Description\n\nDepends on beta");
        AddItem(wsRoot, "Todo", "2-beta.md",
            "# 2 - Beta\n\n## Description\n\nNo deps");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, wsRoot, "health", "check-order", "--fix");

        // Assert
        result.StdErr.Should().BeEmpty(because: "check-order --fix should run cleanly");
        var todoFiles = Directory.GetFiles(Path.Combine(wsRoot, "Todo"), "*.md")
            .Select(Path.GetFileName)
            .Order()
            .ToArray();

        todoFiles.Should().Contain("1-beta.md",
            because: "after --fix, beta (the dependency) should have the lower ID so it comes first");
        todoFiles.Should().Contain("2-alpha.md",
            because: "after --fix, alpha (the dependent) should have the higher ID");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a board directory structure where the config is in the parent of the workspace
    /// root — matching the structure that WorkItemStore.LoadSettings() expects.
    /// </summary>
    private string CreateBoard(string? extraConfigJson = null)
    {
        var parentDir = Path.Combine(Path.GetTempPath(), "mb-ft-" + Guid.NewGuid().ToString("N")[..8]);
        var wsRoot = Path.Combine(parentDir, "board");
        _tempDirs.Add(parentDir);

        foreach (var lane in new[] { "Todo", "In Progress", "Testing", "Done", "ideas", "Rejected" })
        {
            Directory.CreateDirectory(Path.Combine(wsRoot, lane));
        }

        var rootForJson = wsRoot.Replace("\\", "/");
        var extraSection = extraConfigJson != null ? $",\n    {extraConfigJson}" : "";
        var config = $$"""
            {
                "rootPath": "{{rootForJson}}"{{extraSection}}
            }
            """;
        File.WriteAllText(Path.Combine(parentDir, "markban.json"), config, new UTF8Encoding(false));

        return wsRoot;
    }

    private static void AddItem(string wsRoot, string lane, string fileName, string content)
    {
        File.WriteAllText(Path.Combine(wsRoot, lane, fileName), content, new UTF8Encoding(false));
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
