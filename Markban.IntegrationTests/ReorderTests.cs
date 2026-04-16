using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class ReorderTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly TestWorkspace _ws;

    public ReorderTests(ToolBuildFixture build)
    {
        _build = build;
        _ws = new TestWorkspace();
    }

    [Fact]
    public async Task Reorder_RenumbersItemsBySpecifiedOrder()
    {
        // Arrange
        _ws.AddItem("Todo", "1-alpha-task.md", "# 1 - Alpha Task\n\n## Description\n\nFirst");
        _ws.AddItem("Todo", "2-beta-task.md", "# 2 - Beta Task\n\n## Description\n\nSecond");
        _ws.AddItem("Todo", "3-gamma-task.md", "# 3 - Gamma Task\n\n## Description\n\nThird");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "reorder", "Todo", "3,1,2");

        // Assert
        result.StdOut.Should().Contain("file(s) renamed");
        var files = _ws.GetFiles("Todo");
        files.Should().ContainInOrder("1-gamma-task.md", "2-alpha-task.md", "3-beta-task.md");
    }

    [Fact]
    public async Task Reorder_UpdatesHeadingsToMatchNewNumbers()
    {
        // Arrange
        _ws.AddItem("Todo", "1-alpha-task.md", "# 1 - Alpha Task\n\n## Description\n\nFirst");
        _ws.AddItem("Todo", "2-beta-task.md", "# 2 - Beta Task\n\n## Description\n\nSecond");

        // Act
        await CliRunner.RunAsync(_build.DllPath, _ws.Root, "reorder", "Todo", "2,1");

        // Assert
        var betaContent = _ws.ReadFile("Todo", "1-beta-task.md");
        betaContent.Should().StartWith("# 1 - Beta Task", because: "heading should reflect the new number");

        var alphaContent = _ws.ReadFile("Todo", "2-alpha-task.md");
        alphaContent.Should().StartWith("# 2 - Alpha Task", because: "heading should reflect the new number");
    }

    [Fact]
    public async Task Reorder_SubItemsFollowParent()
    {
        // Arrange
        _ws.AddItem("Todo", "1-alpha-task.md", "# 1 - Alpha Task\n\n## Description\n\nFirst");
        _ws.AddItem("Todo", "1a-sub-alpha.md", "# 1a - Sub Alpha\n\n## Description\n\nSub-item");
        _ws.AddItem("Todo", "2-beta-task.md", "# 2 - Beta Task\n\n## Description\n\nSecond");

        // Act
        await CliRunner.RunAsync(_build.DllPath, _ws.Root, "reorder", "Todo", "2,1");

        // Assert
        var files = _ws.GetFiles("Todo");
        files.Should().Contain("1-beta-task.md");
        files.Should().Contain("2-alpha-task.md");
        files.Should().Contain("2a-sub-alpha.md", because: "sub-item should follow parent from 1 to 2");
    }

    [Fact]
    public async Task Reorder_NoSubItems_ExcludesLettered()
    {
        // Arrange
        _ws.AddItem("Todo", "1-alpha-task.md", "# 1 - Alpha Task\n\n## Description\n\nFirst");
        _ws.AddItem("Todo", "1a-sub-alpha.md", "# 1a - Sub Alpha\n\n## Description\n\nSub-item");
        _ws.AddItem("Todo", "2-beta-task.md", "# 2 - Beta Task\n\n## Description\n\nSecond");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "reorder", "Todo", "2,1", "--no-sub-items");

        // Assert
        result.StdOut.Should().Contain("excluded from reorder");
        _ws.FileExists("Todo", "1a-sub-alpha.md").Should().BeTrue(because: "--no-sub-items should leave sub-items at their original names");
    }

    [Fact]
    public async Task Reorder_DryRun_DoesNotRenameFiles()
    {
        // Arrange
        _ws.AddItem("Todo", "1-alpha-task.md", "# 1 - Alpha Task\n\n## Description\n\nFirst");
        _ws.AddItem("Todo", "2-beta-task.md", "# 2 - Beta Task\n\n## Description\n\nSecond");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "reorder", "Todo", "2,1", "--dry-run");

        // Assert
        result.StdOut.Should().Contain("Dry run");
        _ws.FileExists("Todo", "1-alpha-task.md").Should().BeTrue(because: "dry run should not modify files");
        _ws.FileExists("Todo", "2-beta-task.md").Should().BeTrue(because: "dry run should not modify files");
    }

    [Fact]
    public async Task Reorder_InvalidFolder_ReportsError()
    {
        // Arrange — empty workspace

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "reorder", "FakeFolder", "1,2");

        // Assert
        result.StdOut.Should().Contain("Error").And.Contain("invalid");
    }

    [Fact]
    public async Task Reorder_NonExistentItemNumber_ReportsError()
    {
        // Arrange
        _ws.AddItem("Todo", "1-alpha-task.md", "# 1 - Alpha Task\n\n## Description\n\nFirst");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "reorder", "Todo", "1,99");

        // Assert
        result.StdOut.Should().Contain("Error").And.Contain("not found");
    }

    [Fact]
    public async Task Reorder_EmptyFolder_ReportsNoItems()
    {
        // Arrange — no items in Testing folder

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "reorder", "Testing", "1,2");

        // Assert
        result.StdOut.Should().Contain("No work items found");
    }

    [Fact]
    public async Task Reorder_PartialOrder_PutsSpecifiedFirst()
    {
        // Arrange
        _ws.AddItem("Todo", "1-alpha-task.md", "# 1 - Alpha Task\n\n## Description\n\nFirst");
        _ws.AddItem("Todo", "2-beta-task.md", "# 2 - Beta Task\n\n## Description\n\nSecond");
        _ws.AddItem("Todo", "3-gamma-task.md", "# 3 - Gamma Task\n\n## Description\n\nThird");

        // Act — only mention item 3; items 1 and 2 should follow in their original order
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "reorder", "Todo", "3");

        // Assert
        result.StdOut.Should().Contain("file(s) renamed");
        var files = _ws.GetFiles("Todo");
        files.Should().ContainInOrder("1-gamma-task.md", "2-alpha-task.md", "3-beta-task.md");
    }

    public void Dispose() => _ws.Dispose();
}
