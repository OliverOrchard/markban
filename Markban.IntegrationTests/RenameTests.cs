using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class RenameTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly TestWorkspace _ws;

    public RenameTests(ToolBuildFixture build)
    {
        _build = build;
        _ws = new TestWorkspace();
        _ws.AddItem("Todo", "1-original-name.md", "# 1 - Original Name\n\n## Description\n\nThe item to rename");
        _ws.AddItem("Todo", "2-other-item.md", "# 2 - Other Item\n\n## Description\n\nSee [original-name] for details");
    }

    [Fact]
    public async Task Rename_UpdatesFilenameAndH1()
    {
        // Arrange — item 1 exists as 1-original-name.md

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "rename", "1", "Brand New Title");

        // Assert
        result.StdErr.Should().BeEmpty();
        result.StdOut.Should().Contain("brand-new-title", because: "output should confirm the new filename");
        _ws.FileExists("Todo", "1-original-name.md").Should().BeFalse(because: "old file should be removed after rename");
        _ws.FileExists("Todo", "1-brand-new-title.md").Should().BeTrue(because: "file should be renamed to match the new slug");
        var content = _ws.ReadFile("Todo", "1-brand-new-title.md");
        content.Should().StartWith("# 1 - Brand New Title", because: "H1 heading should reflect the new title");
    }

    [Fact]
    public async Task Rename_UpdatesCrossReferencesInOtherFiles()
    {
        // Arrange — item 2 references [original-name]

        // Act
        await CliRunner.RunAsync(_build.DllPath, _ws.Root, "rename", "1", "Brand New Title");

        // Assert
        var otherContent = _ws.ReadFile("Todo", "2-other-item.md");
        otherContent.Should().Contain("[brand-new-title]",
            because: "cross-references to the renamed slug should be updated in other files");
        otherContent.Should().NotContain("[original-name]",
            because: "the old slug reference should be replaced");
    }

    [Fact]
    public async Task Rename_BySlug_WorksAsWellAsById()
    {
        // Arrange — identify item by slug rather than numeric ID

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "rename", "original-name", "Slug Renamed");

        // Assert
        result.StdErr.Should().BeEmpty();
        _ws.FileExists("Todo", "1-slug-renamed.md").Should().BeTrue(because: "rename should work when identified by slug");
    }

    [Fact]
    public async Task Rename_DryRun_PrintsPreviewAndMakesNoChanges()
    {
        // Arrange — item exists on disk with its original name

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "rename", "1", "Dry Run Name", "--dry-run");

        // Assert
        result.StdErr.Should().BeEmpty();
        result.StdOut.Should().Contain("Would rename", because: "dry-run should show the file rename preview");
        result.StdOut.Should().Contain("Would update H1", because: "dry-run should show the H1 update preview");
        result.StdOut.Should().Contain("Would update cross-references", because: "dry-run should show the cross-ref update preview");
        _ws.FileExists("Todo", "1-original-name.md").Should().BeTrue(because: "dry-run must not rename the file on disk");
        _ws.FileExists("Todo", "1-dry-run-name.md").Should().BeFalse(because: "dry-run must not create the new file");
    }

    [Fact]
    public async Task Rename_DryRun_DoesNotUpdateCrossReferences()
    {
        // Arrange — item 2 references [original-name]

        // Act
        await CliRunner.RunAsync(_build.DllPath, _ws.Root, "rename", "1", "Dry Run Name", "--dry-run");

        // Assert
        var otherContent = _ws.ReadFile("Todo", "2-other-item.md");
        otherContent.Should().Contain("[original-name]",
            because: "dry-run must not modify cross-references in other files");
        otherContent.Should().NotContain("[dry-run-name]");
    }

    [Fact]
    public async Task Rename_NonExistentItem_ReportsError()
    {
        // Arrange — no item with ID 999

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "rename", "999", "Does Not Matter");

        // Assert
        result.StdErr.Should().Contain("Error").And.Contain("not found",
            because: "renaming a non-existent item should give a clear error");
    }

    [Fact]
    public async Task Rename_WithHeadingDisabled_DoesNotUpdateH1()
    {
        // Arrange: board with heading.enabled: false
        var parentDir = Path.Combine(Path.GetTempPath(), "mb-rename-noheading-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(parentDir);
        var boardRoot = Path.Combine(parentDir, "board");
        Directory.CreateDirectory(Path.Combine(boardRoot, "Todo"));
        File.WriteAllText(
            Path.Combine(boardRoot, "Todo", "1-no-heading.md"),
            "## Description\n\nNo H1 here");
        File.WriteAllText(
            Path.Combine(parentDir, "markban.json"),
            "{\"rootPath\": \"./board\", \"heading\": {\"enabled\": false}}");

        // Act
        var result = await CliRunner.RunInDirAsync(
            _build.DllPath, parentDir, "rename", "no-heading", "Heading Off Renamed");

        // Assert
        result.StdErr.Should().BeEmpty();
        result.StdOut.Should().NotContain("Would update H1",
            because: "when heading is disabled, the H1 update step should be omitted from output");
        var newContent = File.ReadAllText(Path.Combine(boardRoot, "Todo", "1-heading-off-renamed.md"));
        newContent.Should().NotContain("# 1 -", because: "heading.enabled:false means no H1 is written");

        // Cleanup
        Directory.Delete(parentDir, true);
    }

    public void Dispose() => _ws.Dispose();
}
