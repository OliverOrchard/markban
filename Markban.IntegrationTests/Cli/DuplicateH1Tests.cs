using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Xunit;

namespace Markban.IntegrationTests;

/// <summary>
/// Item 28 — Verifies that commands never produce duplicate H1 headings when files
/// already contain YAML frontmatter (e.g. blocked, tags, or dependsOn fields).
/// </summary>
[Collection("CLI")]
public class DuplicateH1Tests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly TestWorkspace _ws;

    private const string FrontmatterHeader = "---\ntags: [feature]\n# yaml comment that looks like a heading\n---\n\n";

    public DuplicateH1Tests(ToolBuildFixture build)
    {
        _build = build;
        _ws = new TestWorkspace();
    }

    // ------------------------------------------------------------------ rename

    [Fact]
    public async Task Rename_WithFrontmatter_UpdatesBodyH1_NotYamlComment()
    {
        // Arrange
        _ws.AddItem("Todo", "1-my-task.md",
            FrontmatterHeader + "# 1 - My Task\n\n## Description\n\nSome content");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "rename", "1", "New Title");

        // Assert
        result.StdErr.Should().BeEmpty();
        var content = _ws.ReadFile("Todo", "1-new-title.md");
        content.Should().Contain("# 1 - New Title",
            because: "H1 in the body should be updated to the new title");
        content.Should().NotContain("# 1 - My Task",
            because: "old H1 should be replaced");
        content.Should().Contain("# yaml comment that looks like a heading",
            because: "YAML comment inside frontmatter must not be modified");
        var h1Count = content.Split('\n').Count(l => l.TrimEnd('\r').TrimStart().StartsWith("# "));
        h1Count.Should().Be(2,
            because: "there should be exactly the frontmatter comment line and the body H1 — no duplicates added");
    }

    [Fact]
    public async Task Rename_WithFrontmatter_DoesNotAddDuplicateH1()
    {
        // Arrange
        _ws.AddItem("Todo", "2-alpha.md",
            "---\nblocked: waiting\n---\n\n# 2 - Alpha\n\n## Description\n\nContent");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "rename", "2", "Renamed Alpha");

        // Assert
        result.StdErr.Should().BeEmpty();
        var content = _ws.ReadFile("Todo", "2-renamed-alpha.md");
        var h1Lines = content.Split('\n').Where(l => l.TrimEnd('\r').TrimStart().StartsWith("# ")).Select(l => l.TrimEnd('\r')).ToList();
        h1Lines.Should().ContainSingle(because: "rename must produce exactly one H1 heading");
        h1Lines[0].Should().Be("# 2 - Renamed Alpha",
            because: "the single H1 should be the updated title");
    }

    // ------------------------------------------------------------------ reorder

    [Fact]
    public async Task Reorder_WithFrontmatter_UpdatesBodyH1()
    {
        // Arrange
        _ws.AddItem("Todo", "1-first.md",
            "---\ntags: [bug]\n---\n\n# 1 - First\n\n## Description\n\nFirst task");
        _ws.AddItem("Todo", "2-second.md",
            "---\ntags: [feature]\n---\n\n# 2 - Second\n\n## Description\n\nSecond task");

        // Act — reorder 2 before 1
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "reorder", "Todo", "2,1");

        // Assert
        result.StdErr.Should().BeEmpty();

        var firstFileContent = _ws.ReadFile("Todo", "1-second.md");
        firstFileContent.Should().Contain("# 1 - Second",
            because: "reordering should update the body H1 to the new ID");
        firstFileContent.Should().NotContain("# 2 - Second",
            because: "old ID should not remain in the H1");

        var h1Lines = firstFileContent.Split('\n').Where(l => l.TrimEnd('\r').TrimStart().StartsWith("# ")).ToList();
        h1Lines.Should().ContainSingle(because: "reorder must produce exactly one H1 heading");
    }

    // ------------------------------------------------------------------ create with --priority (triggers ShiftIdsUp)

    [Fact]
    public async Task Create_WithPriorityAndFrontmatter_ShiftedItemKeepsOneH1()
    {
        // Arrange — item with frontmatter that will be shifted when a priority item is inserted
        _ws.AddItem("Todo", "1-existing.md",
            "---\ntags: [chore]\n---\n\n# 1 - Existing\n\n## Description\n\nExisting task");

        // Act — create a new item at priority (shifts existing 1 → 2)
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "create", "Priority Task", "--priority");

        // Assert
        result.StdErr.Should().BeEmpty();

        // The shifted item should now be 2-existing.md
        _ws.FileExists("Todo", "2-existing.md").Should().BeTrue(
            because: "existing item should be shifted to ID 2");
        var shiftedContent = _ws.ReadFile("Todo", "2-existing.md");
        var h1Lines = shiftedContent.Split('\n').Where(l => l.TrimEnd('\r').TrimStart().StartsWith("# ")).Select(l => l.TrimEnd('\r')).ToList();
        h1Lines.Should().ContainSingle(because: "shifted item must have exactly one H1 heading");
        h1Lines[0].Should().Be("# 2 - Existing",
            because: "H1 should reflect the new ID after shifting");
    }

    // ------------------------------------------------------------------ sanitize

    [Fact]
    public async Task Sanitize_FixesDuplicateH1()
    {
        // Arrange — file has two H1 headings; sanitize should remove the second
        _ws.AddItem("Todo", "1-my-task.md",
            "# 1 - My Task\n\n## Description\n\nSome content\n\n# Stale Duplicate Heading\n\nMore content");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "sanitize");

        // Assert
        result.StdErr.Should().BeEmpty();
        var content = _ws.ReadFile("Todo", "1-my-task.md");
        var h1Lines = content.Split('\n').Where(l => l.TrimEnd('\r').StartsWith("# ")).ToList();
        h1Lines.Should().ContainSingle(because: "sanitize must remove duplicate H1 headings");
        h1Lines[0].TrimEnd('\r').Should().Be("# 1 - My Task",
            because: "the first H1 should be preserved");
    }

    [Fact]
    public async Task Sanitize_FixesDuplicateH1_WithFrontmatter()
    {
        // Arrange — file has frontmatter + two H1s; frontmatter must be preserved
        _ws.AddItem("Todo", "2-task.md",
            "---\ntags: [bug]\n---\n\n# 2 - Task\n\n## Description\n\nContent\n\n# Oops Another H1\n\nAfter");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "sanitize");

        // Assert
        result.StdErr.Should().BeEmpty();
        var content = _ws.ReadFile("Todo", "2-task.md");
        content.Should().Contain("tags: [bug]",
            because: "frontmatter must be preserved");
        var h1Lines = content.Split('\n').Where(l => l.TrimEnd('\r').StartsWith("# ")).ToList();
        h1Lines.Should().ContainSingle(because: "sanitize must remove duplicate H1 even when frontmatter is present");
    }

    [Fact]
    public async Task Sanitize_FixesDuplicateH1_PrefersCanonicalOverStaleFirst()
    {
        // Arrange — stale H1 appears first, canonical H1 (matching ID) appears second
        _ws.AddItem("Todo", "3-new-title.md",
            "# Old Stale Title\n\n## Description\n\nSome content\n\n# 3 - New Title\n\nMore content");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "sanitize");

        // Assert
        result.StdErr.Should().BeEmpty();
        var content = _ws.ReadFile("Todo", "3-new-title.md");
        var h1Lines = content.Split('\n').Where(l => l.TrimEnd('\r').StartsWith("# ")).ToList();
        h1Lines.Should().ContainSingle(because: "sanitize must reduce to exactly one H1");
        h1Lines[0].TrimEnd('\r').Should().Be("# 3 - New Title",
            because: "sanitize should prefer the canonical H1 matching the item ID over a stale first H1");
    }

    [Fact]
    public async Task Sanitize_FixesDuplicateH1_PrefersSlugMatchingH1_WhenSecondH1HasNoIdPrefix()
    {
        // Arrange — first H1 is stale (no ID prefix, no slug match), second H1 text is exactly the item slug
        _ws.AddItem("Todo", "12-cross-ref-test-item.md",
            "# Stale Title\n\n## Description\n\nSome content\n\n# cross-ref-test-item\n\nMore content");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "sanitize");

        // Assert
        result.StdErr.Should().BeEmpty();
        var content = _ws.ReadFile("Todo", "12-cross-ref-test-item.md");
        var h1Lines = content.Split('\n').Where(l => l.TrimEnd('\r').StartsWith("# ")).ToList();
        h1Lines.Should().ContainSingle(because: "sanitize must reduce to exactly one H1");
        h1Lines[0].TrimEnd('\r').Should().Be("# cross-ref-test-item",
            because: "sanitize should prefer the H1 whose text matches the filename slug over a stale first H1");
    }

    public void Dispose()
    {
        _ws.Dispose();
    }
}
