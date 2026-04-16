using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Xunit;

namespace Markban.IntegrationTests;

/// <summary>
/// Tests that markban.json config files are respected and that multiple boards on the same
/// machine operate independently without interfering with each other.
/// </summary>
[Collection("CLI")]
public class ConfigAndMultipleBoardsTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly List<IDisposable> _workspaces = [];

    public ConfigAndMultipleBoardsTests(ToolBuildFixture build)
    {
        _build = build;
    }

    private TestWorkspace NewWorkspace()
    {
        var ws = new TestWorkspace();
        _workspaces.Add(ws);
        return ws;
    }

    // -------------------------------------------------------------------------
    // Config file rootPath discovery
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Config_RelativeRootPath_IsRespectedByCli()
    {
        // Arrange: workspace uses a non-default folder name; config points to it
        var ws = NewWorkspace();
        ws.AddItem("Todo", "1-config-item.md", "# 1 - Config Item\n\n## Description\n\nFound via config");

        // projectDir/markban.json  →  rootPath: "<absolute path to ws.Root>"
        var projectDir = ws.CreateProjectDir();

        // Act: run the CLI from the project dir WITHOUT --root
        var result = await CliRunner.RunInDirAsync(_build.DllPath, projectDir, "list", "--summary");

        // Assert: board was found through the config file
        result.StdErr.Should().BeEmpty(because: "CLI should locate the board via markban.json without --root");
        var items = JsonHelper.DeserializeSummaries(result.StdOut);
        items.Should().ContainSingle()
            .Which.Slug.Should().Be("config-item",
                because: "rootPath in markban.json should point to the workspace containing this item");
    }

    [Fact]
    public async Task Config_RootPath_CanUseRelativeNotation()
    {
        // Arrange: project dir with markban.json using a relative ./boards path
        var projectDir = Path.Combine(Path.GetTempPath(), "mb-reltest-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(projectDir);
        _workspaces.Add(new _TempDirWorkspace(projectDir)); // register for cleanup

        var boardsDir = Path.Combine(projectDir, "boards");
        foreach (var sub in new[] { "Todo", "In Progress", "Testing", "Done", "ideas", "Rejected" })
        {
            Directory.CreateDirectory(Path.Combine(boardsDir, sub));
        }

        File.WriteAllText(Path.Combine(boardsDir, "Todo", "1-relative-path.md"),
            "# 1 - Relative Path\n\n## Description\n\nFound via relative config");
        File.WriteAllText(Path.Combine(projectDir, "markban.json"), """{"rootPath": "./boards"}""");

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, projectDir, "list", "--summary");

        // Assert
        result.StdErr.Should().BeEmpty();
        var items = JsonHelper.DeserializeSummaries(result.StdOut);
        items.Should().ContainSingle().Which.Slug.Should().Be("relative-path");
    }

    [Fact]
    public async Task Config_WithoutRootPath_FallsBackToWorkItemsDiscovery()
    {
        // Arrange: markban.json exists but has no rootPath; standard work-items/ should still be found
        var projectDir = Path.Combine(Path.GetTempPath(), "mb-norp-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(projectDir);
        _workspaces.Add(new _TempDirWorkspace(projectDir));

        var workItemsDir = Path.Combine(projectDir, "work-items");
        foreach (var sub in new[] { "Todo", "In Progress", "Testing", "Done", "ideas", "Rejected" })
        {
            Directory.CreateDirectory(Path.Combine(workItemsDir, sub));
        }

        File.WriteAllText(Path.Combine(workItemsDir, "Todo", "1-fallback.md"),
            "# 1 - Fallback\n\n## Description\n\nFound via fallback");
        // Config with other settings but NO rootPath
        File.WriteAllText(Path.Combine(projectDir, "markban.json"), """{"git": {"enabled": false}}""");

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, projectDir, "list", "--summary");

        // Assert
        result.StdErr.Should().BeEmpty(because: "CLI should fall back to work-items/ discovery when rootPath is absent");
        var items = JsonHelper.DeserializeSummaries(result.StdOut);
        items.Should().ContainSingle().Which.Slug.Should().Be("fallback");
    }

    [Fact]
    public async Task NoConfig_FallsBackToWorkItemsDiscovery()
    {
        // Arrange: no markban.json at all; standard work-items/ in the working directory
        var projectDir = Path.Combine(Path.GetTempPath(), "mb-noconf-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(projectDir);
        _workspaces.Add(new _TempDirWorkspace(projectDir));

        var workItemsDir = Path.Combine(projectDir, "work-items");
        foreach (var sub in new[] { "Todo", "In Progress", "Testing", "Done", "ideas", "Rejected" })
        {
            Directory.CreateDirectory(Path.Combine(workItemsDir, sub));
        }

        File.WriteAllText(Path.Combine(workItemsDir, "Todo", "1-no-config.md"),
            "# 1 - No Config\n\n## Description\n\nFound without any config");

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, projectDir, "list", "--summary");

        // Assert
        result.StdErr.Should().BeEmpty(because: "existing walk-up discovery should still work without any config file");
        var items = JsonHelper.DeserializeSummaries(result.StdOut);
        items.Should().ContainSingle().Which.Slug.Should().Be("no-config");
    }

    [Fact]
    public async Task Config_InParentDir_IsFoundByWalkUp()
    {
        // Arrange: markban.json is at a parent level; CLI is run from a subdirectory
        var projectDir = Path.Combine(Path.GetTempPath(), "mb-walkup-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(projectDir);
        _workspaces.Add(new _TempDirWorkspace(projectDir));

        var ws = new TestWorkspace();
        _workspaces.Add(ws);
        var subDir = Path.Combine(projectDir, "src", "feature");
        Directory.CreateDirectory(subDir);

        // Config lives at projectDir but we'll run the CLI from projectDir/src/feature
        File.WriteAllText(Path.Combine(projectDir, "markban.json"),
            $$"""{"rootPath": "{{ws.Root.Replace("\\", "/")}}"}""");

        ws.AddItem("Todo", "1-walk-up.md", "# 1 - Walk Up\n\n## Description\n\nFound via parent config");

        // Act: run from the deep subdirectory — FindRoot() must walk up to projectDir
        var result = await CliRunner.RunInDirAsync(_build.DllPath, subDir, "list", "--summary");

        // Assert
        result.StdErr.Should().BeEmpty(because: "walk-up should find markban.json in the parent directory");
        var items = JsonHelper.DeserializeSummaries(result.StdOut);
        items.Should().ContainSingle().Which.Slug.Should().Be("walk-up");
    }

    // -------------------------------------------------------------------------
    // Multiple boards on the same machine
    // -------------------------------------------------------------------------

    [Fact]
    public async Task MultipleBoards_EachBoardUsesItsOwnConfig_List()
    {
        // Arrange: two completely separate project trees, each with their own markban.json
        var wsA = NewWorkspace();
        wsA.AddItem("Todo", "1-board-a.md", "# 1 - Board A\n\n## Description\n\nItem on board A");
        wsA.AddItem("In Progress", "2-board-a-wip.md", "# 2 - Board A WIP\n\n## Description\n\nWIP on board A");
        var projectA = wsA.CreateProjectDir();

        var wsB = NewWorkspace();
        wsB.AddItem("Todo", "1-board-b.md", "# 1 - Board B\n\n## Description\n\nItem on board B");
        var projectB = wsB.CreateProjectDir();

        // Act: list each board independently
        var resultA = await CliRunner.RunInDirAsync(_build.DllPath, projectA, "list", "--summary");
        var resultB = await CliRunner.RunInDirAsync(_build.DllPath, projectB, "list", "--summary");

        // Assert: each board sees only its own items
        resultA.StdErr.Should().BeEmpty();
        resultB.StdErr.Should().BeEmpty();

        var itemsA = JsonHelper.DeserializeSummaries(resultA.StdOut);
        var itemsB = JsonHelper.DeserializeSummaries(resultB.StdOut);

        itemsA.Should().HaveCount(2, because: "board A has 2 items and should not see board B's items");
        itemsA.Should().OnlyContain(i => i.Slug.Contains("board-a"),
            because: "board A items should only be from board A");

        itemsB.Should().ContainSingle(because: "board B has 1 item and should not see board A's items")
            .Which.Slug.Should().Be("board-b");
    }

    [Fact]
    public async Task MultipleBoards_CreateOnOneBoard_DoesNotAffectOther()
    {
        // Arrange: two independent boards
        var wsA = NewWorkspace();
        wsA.AddItem("Todo", "1-existing.md", "# 1 - Existing\n\n## Description\n\nPre-existing item");
        var projectA = wsA.CreateProjectDir();

        var wsB = NewWorkspace();
        wsB.AddItem("Todo", "1-separate.md", "# 1 - Separate\n\n## Description\n\nItem in board B");
        var projectB = wsB.CreateProjectDir();

        // Act: create a new item on board A only
        var createResult = await CliRunner.RunInDirAsync(_build.DllPath, projectA, "create", "New On A");

        // Assert: item was created on board A
        createResult.StdOut.Should().Contain("Successfully created",
            because: "the create command should succeed for board A");
        wsA.FileExists("Todo", "2-new-on-a.md").Should().BeTrue(because: "new item should appear in board A's directory");

        // Assert: board B is completely untouched
        wsB.GetFiles("Todo").Should().ContainSingle()
            .And.Contain("1-separate.md",
                because: "creating an item on board A must not affect board B");
    }

    [Fact]
    public async Task MultipleBoards_MoveOnOneBoard_DoesNotAffectOther()
    {
        // Arrange
        var wsA = NewWorkspace();
        wsA.AddItem("Todo", "1-alpha.md", "# 1 - Alpha\n\n## Description\n\nAlpha item");
        var projectA = wsA.CreateProjectDir();

        var wsB = NewWorkspace();
        wsB.AddItem("Todo", "1-beta.md", "# 1 - Beta\n\n## Description\n\nBeta item");
        var projectB = wsB.CreateProjectDir();

        // Act: move item 1 on board A to In Progress
        var moveResult = await CliRunner.RunInDirAsync(_build.DllPath, projectA, "move", "1", "InProgress");

        // Assert: board A item moved
        moveResult.StdOut.Should().Contain("Successfully moved",
            because: "move should succeed on board A");
        wsA.FileExists("In Progress", "1-alpha.md").Should().BeTrue(because: "item should now be in In Progress on board A");
        wsA.FileExists("Todo", "1-alpha.md").Should().BeFalse(because: "item should no longer be in Todo on board A");

        // Assert: board B untouched
        wsB.FileExists("Todo", "1-beta.md").Should().BeTrue(because: "board B should be unaffected by board A's move");
    }

    // -------------------------------------------------------------------------
    // Commit tag validation uses configured list
    // -------------------------------------------------------------------------

    [Fact]
    public async Task CommitTag_InvalidTag_ErrorListsConfiguredTagsNotDefaults()
    {
        // Arrange: board with a custom commit.tags list that excludes 'style'
        var parentDir = Path.Combine(Path.GetTempPath(), "mb-committag-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(parentDir);
        _workspaces.Add(new _TempDirWorkspace(parentDir));

        var boardRoot = Path.Combine(parentDir, "board");
        foreach (var lane in new[] { "Todo", "In Progress", "Testing", "Done", "ideas", "Rejected" })
        {
            Directory.CreateDirectory(Path.Combine(boardRoot, lane));
        }

        File.WriteAllText(
            Path.Combine(boardRoot, "Todo", "1-target-item.md"),
            "# 1 - Target Item\n\n## Description\n\nItem to commit");
        File.WriteAllText(
            Path.Combine(parentDir, "markban.json"),
            "{\"rootPath\": \"./board\", \"commit\": {\"tags\": [\"feat\", \"fix\", \"docs\", \"chore\", \"release\"]}}");

        // Act: use 'style' which is in the default list but not the configured one
        var result = await CliRunner.RunInDirAsync(
            _build.DllPath, parentDir, "commit", "1", "--tag", "style", "--message", "Testing tag validation", "--dry-run");

        // Assert
        result.StdErr.Should().Contain("Invalid tag 'style'",
            because: "'style' is not in the configured tag list");
        result.StdErr.Should().Contain("release",
            because: "the error message should list the configured tags, which include 'release'");
        result.StdErr.Should().NotContain("refactor",
            because: "the error should not list tags from the hardcoded defaults that are absent from config");
    }

    [Fact]
    public async Task CommitTag_CustomTag_IsAccepted()
    {
        // Arrange: board with 'release' as a custom tag
        var parentDir = Path.Combine(Path.GetTempPath(), "mb-customtag-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(parentDir);
        _workspaces.Add(new _TempDirWorkspace(parentDir));

        var boardRoot = Path.Combine(parentDir, "board");
        foreach (var lane in new[] { "Todo", "In Progress", "Testing", "Done", "ideas", "Rejected" })
        {
            Directory.CreateDirectory(Path.Combine(boardRoot, lane));
        }

        File.WriteAllText(
            Path.Combine(boardRoot, "Todo", "1-release-item.md"),
            "# 1 - Release Item\n\n## Description\n\nTo be released");
        File.WriteAllText(
            Path.Combine(parentDir, "markban.json"),
            "{\"rootPath\": \"./board\", \"commit\": {\"tags\": [\"feat\", \"fix\", \"docs\", \"chore\", \"release\"]}}");

        // Act: 'release' is a custom tag in config; use --dry-run so no git calls are made
        var result = await CliRunner.RunInDirAsync(
            _build.DllPath, parentDir, "commit", "1", "--tag", "release", "--message", "Ship it", "--dry-run");

        // Assert
        result.StdErr.Should().NotContain("Invalid tag",
            because: "'release' is explicitly listed in commit.tags and should be accepted");
    }

    // -------------------------------------------------------------------------
    // H1 heading config
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_WithHeadingDisabled_ProducesFileWithNoH1()
    {
        // Arrange: board with heading.enabled: false
        var parentDir = Path.Combine(Path.GetTempPath(), "mb-noheading-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(parentDir);
        _workspaces.Add(new _TempDirWorkspace(parentDir));

        var boardRoot = Path.Combine(parentDir, "board");
        Directory.CreateDirectory(Path.Combine(boardRoot, "Todo"));
        File.WriteAllText(
            Path.Combine(parentDir, "markban.json"),
            "{\"rootPath\": \"./board\", \"heading\": {\"enabled\": false}}");

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, parentDir, "create", "No Heading Item");

        // Assert
        result.StdErr.Should().BeEmpty();
        var files = Directory.GetFiles(Path.Combine(boardRoot, "Todo"), "*.md");
        files.Should().ContainSingle();
        var content = File.ReadAllText(files[0]);
        content.Should().NotContain("# 1 -",
            because: "heading.enabled:false should mean no H1 is written to the file");
    }

    // -------------------------------------------------------------------------
    // Slug casing config
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_WithInvalidSlugCasing_ReportsError()
    {
        // Arrange: board with an invalid slugs.casing value
        var parentDir = Path.Combine(Path.GetTempPath(), "mb-badcasing-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(parentDir);
        _workspaces.Add(new _TempDirWorkspace(parentDir));

        var boardRoot = Path.Combine(parentDir, "board");
        Directory.CreateDirectory(Path.Combine(boardRoot, "Todo"));
        File.WriteAllText(
            Path.Combine(parentDir, "markban.json"),
            "{\"rootPath\": \"./board\", \"slugs\": {\"casing\": \"invalid\"}}");

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, parentDir, "create", "Some Item");

        // Assert
        result.StdErr.Should().Contain("Error",
            because: "an invalid casing value should be reported as an error");
        result.StdErr.Should().Contain("invalid",
            because: "the error should echo back the bad casing value");
        result.StdErr.Should().Contain("kebab",
            because: "the error should list the valid casing options");
    }

    [Fact]
    public async Task Create_WithSnakeCasing_ProducesUnderscoreSlug()
    {
        // Arrange: board with snake_case slugs
        var parentDir = Path.Combine(Path.GetTempPath(), "mb-snake-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(parentDir);
        _workspaces.Add(new _TempDirWorkspace(parentDir));

        var boardRoot = Path.Combine(parentDir, "board");
        Directory.CreateDirectory(Path.Combine(boardRoot, "Todo"));
        File.WriteAllText(
            Path.Combine(parentDir, "markban.json"),
            "{\"rootPath\": \"./board\", \"slugs\": {\"casing\": \"snake\"}}");

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, parentDir, "create", "My Work Item Title");

        // Assert
        result.StdErr.Should().BeEmpty();
        var files = Directory.GetFiles(Path.Combine(boardRoot, "Todo"), "*.md");
        files.Should().ContainSingle();
        Path.GetFileName(files[0]).Should().Be("1-my_work_item_title.md",
            because: "snake casing should separate words with underscores");
    }

    // -------------------------------------------------------------------------
    // Help text respects config
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HelpCommit_ShowsConfiguredTagsNotHardcodedDefaults()
    {
        // Arrange: markban.json lives beside the work-items root so LoadSettings can find it
        var parentDir = Path.Combine(Path.GetTempPath(), "mb-helptags-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(parentDir);
        _workspaces.Add(new _TempDirWorkspace(parentDir));

        var boardRoot = Path.Combine(parentDir, "work-items-root");
        foreach (var lane in new[] { "Todo", "In Progress", "Testing", "Done", "ideas", "Rejected" })
        {
            Directory.CreateDirectory(Path.Combine(boardRoot, lane));
        }

        File.WriteAllText(
            Path.Combine(parentDir, "markban.json"),
            "{\"rootPath\": \"./work-items-root\", \"commit\": {\"tags\": [\"feat\", \"fix\", \"docs\", \"chore\", \"release\"]}}");

        // Act
        var result = await CliRunner.RunInDirAsync(_build.DllPath, parentDir, "help", "commit");

        // Assert
        result.StdOut.Should().Contain("release",
            because: "help commit should show the tags from commit.tags config, which includes 'release'");
        result.StdOut.Should().NotContain("style",
            because: "'style' is in the default tag list but not in the configured list");
    }

    // -------------------------------------------------------------------------
    // Cleanup
    // -------------------------------------------------------------------------

    public void Dispose()
    {
        foreach (var ws in _workspaces)
        {
            ws.Dispose();
        }
    }

    // Minimal IDisposable wrapper so arbitrary temp dirs can be tracked in _workspaces
    private sealed class _TempDirWorkspace(string dir) : IDisposable
    {
        public void Dispose()
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
