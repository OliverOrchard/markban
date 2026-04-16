using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class MoveTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly TestWorkspace _ws;

    public MoveTests(ToolBuildFixture build)
    {
        _build = build;
        _ws = new TestWorkspace();

        _ws.AddItem("Todo", "1-first-task.md", "# 1 - First Task\n\n## Description\n\nA task");
        _ws.AddItem("Todo", "2-second-task.md", "# 2 - Second Task\n\n## Description\n\nAnother task");
        _ws.AddItem("In Progress", "3-active-task.md", "# 3 - Active Task\n\n## Description\n\nIn progress");
        _ws.AddItem("ideas", "cool-idea.md", "# Cool Idea\n\n## Description\n\nAn idea");
    }

    [Fact]
    public async Task Move_BetweenNumberedLanes()
    {
        // Arrange — workspace seeded in constructor; item 1 is in Todo

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "move", "1", "In Progress");

        // Assert
        result.StdOut.Should().Contain("Successfully moved");
        _ws.FileExists("Todo", "1-first-task.md").Should().BeFalse(because: "file should be removed from source lane");
        _ws.FileExists("In Progress", "1-first-task.md").Should().BeTrue(because: "file should appear in target lane");
    }

    [Fact]
    public async Task Move_ToIdeas_StripsNumberPrefix()
    {
        // Arrange — workspace seeded in constructor; item 1 is in Todo

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "move", "1", "Ideas");

        // Assert
        result.StdOut.Should().Contain("Successfully moved");
        _ws.FileExists("Todo", "1-first-task.md").Should().BeFalse();
        _ws.FileExists("ideas", "first-task.md").Should().BeTrue(because: "Ideas items are slug-only; number prefix should be stripped");
    }

    [Fact]
    public async Task Move_ToRejected_StripsNumberPrefix()
    {
        // Arrange — workspace seeded in constructor; item 2 is in Todo

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "move", "2", "Rejected");

        // Assert
        result.StdOut.Should().Contain("Successfully moved");
        _ws.FileExists("Todo", "2-second-task.md").Should().BeFalse();
        _ws.FileExists("Rejected", "second-task.md").Should().BeTrue(because: "Rejected items are slug-only; number prefix should be stripped");
    }

    [Fact]
    public async Task Move_FromIdeas_AssignsNextNumber()
    {
        // Arrange — workspace seeded in constructor; max numbered item is 3

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "move", "cool-idea", "Todo");

        // Assert
        result.StdOut.Should().Contain("Successfully moved");
        _ws.FileExists("ideas", "cool-idea.md").Should().BeFalse();
        _ws.FileExists("Todo", "4-cool-idea.md").Should().BeTrue(because: "promoted idea gets next safe number (max 3 + 1 = 4)");
    }

    [Fact]
    public async Task Move_NonExistentItem_ReportsError()
    {
        // Arrange — workspace seeded in constructor

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "move", "nonexistent", "Todo");

        // Assert
        result.StdOut.Should().Contain("Error").And.Contain("not found");
    }

    [Fact]
    public async Task Move_ToSameLane_ReportsAlreadyThere()
    {
        // Arrange — workspace seeded in constructor; item 1 is already in Todo

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "move", "1", "Todo");

        // Assert
        result.StdOut.Should().Contain("already in");
    }

    [Fact]
    public async Task Move_ToInvalidLane_ReportsError()
    {
        // Arrange — workspace seeded in constructor

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "move", "1", "InvalidLane");

        // Assert
        result.StdOut.Should().Contain("Error").And.Contain("invalid");
    }

    [Fact]
    public async Task Move_ToIdeas_CompactsSourceFolder()
    {
        // Arrange — workspace seeded in constructor; items 1 and 2 in Todo

        // Act
        await CliRunner.RunAsync(_build.DllPath, _ws.Root, "move", "1", "Ideas");

        // Assert
        var files = _ws.GetFiles("Todo");
        files.Should().ContainSingle(because: "after removing item 1 the remaining item should stay");
    }

    [Fact]
    public async Task Move_ToIdeas_CompactionClosesInternalGaps()
    {
        // Arrange — three items in Todo so removing the middle one creates an internal gap
        var ws2 = new TestWorkspace();
        ws2.AddItem("Todo", "1-alpha.md", "# 1 - Alpha\n\n## Description\n\nFirst");
        ws2.AddItem("Todo", "2-beta.md", "# 2 - Beta\n\n## Description\n\nSecond");
        ws2.AddItem("Todo", "3-gamma.md", "# 3 - Gamma\n\n## Description\n\nThird");

        try
        {
            // Act — remove item 2; [1, 3] has a gap, should compact to [1, 2]
            await CliRunner.RunAsync(_build.DllPath, ws2.Root, "move", "2", "Ideas");

            // Assert — item 1 untouched, item 3 shifted down to fill the gap
            ws2.FileExists("Todo", "1-alpha.md").Should().BeTrue(because: "item below the gap keeps its number");
            ws2.FileExists("Todo", "2-gamma.md").Should().BeTrue(because: "item above the gap should shift down to 2");
        }
        finally
        {
            ws2.Dispose();
        }
    }

    [Fact]
    public async Task Move_ToRejected_CompactionIgnoresOtherFolderNumbers()
    {
        // Arrange — three items in a fresh workspace, one high-numbered item in Done
        var ws2 = new TestWorkspace();
        ws2.AddItem("Todo", "10-aaa.md", "# 10 - Aaa\n\n## Description\n\nTask A");
        ws2.AddItem("Todo", "11-bbb.md", "# 11 - Bbb\n\n## Description\n\nTask B");
        ws2.AddItem("Todo", "12-ccc.md", "# 12 - Ccc\n\n## Description\n\nTask C");
        ws2.AddItem("Done", "50-old-done.md", "# 50 - Old Done\n\n## Description\n\nDone");

        try
        {
            // Act — remove middle item; should compact [10, 12] -> [10, 11]
            await CliRunner.RunAsync(_build.DllPath, ws2.Root, "move", "11", "Rejected");

            // Assert — compacted numbers stay in their original range, unaffected by Done item 50
            ws2.FileExists("Todo", "10-aaa.md").Should().BeTrue(because: "item below the gap should keep its number");
            ws2.FileExists("Todo", "11-ccc.md").Should().BeTrue(because: "item above the gap should shift down to fill it, not jump to 51");
        }
        finally
        {
            ws2.Dispose();
        }
    }

    [Fact]
    public async Task Move_ToIdeas_CompactionUpdatesHeadings()
    {
        // Arrange — four items so removing the second creates a gap with two items to shift
        var ws2 = new TestWorkspace();
        ws2.AddItem("Todo", "5-alpha.md", "# 5 - Alpha\n\n## Description\n\nFirst");
        ws2.AddItem("Todo", "6-beta.md", "# 6 - Beta\n\n## Description\n\nSecond");
        ws2.AddItem("Todo", "7-gamma.md", "# 7 - Gamma\n\n## Description\n\nThird");
        ws2.AddItem("Todo", "8-delta.md", "# 8 - Delta\n\n## Description\n\nFourth");

        try
        {
            // Act — remove item 6; [5, 7, 8] has a gap, should compact to [5, 6, 7]
            await CliRunner.RunAsync(_build.DllPath, ws2.Root, "move", "6", "Ideas");

            // Assert — headings inside compacted files should match new numbers
            ws2.FileExists("Todo", "5-alpha.md").Should().BeTrue(because: "item below gap is untouched");

            var gammaContent = ws2.ReadFile("Todo", "6-gamma.md");
            gammaContent.Should().StartWith("# 6 - Gamma", because: "the heading should be updated to match the compacted number");

            var deltaContent = ws2.ReadFile("Todo", "7-delta.md");
            deltaContent.Should().StartWith("# 7 - Delta", because: "the heading should be updated to match the compacted number");
        }
        finally
        {
            ws2.Dispose();
        }
    }

    [Fact]
    public async Task Move_BySlug_ForNumberedItem()
    {
        // Arrange — workspace seeded in constructor; item 1 is 'first-task' in Todo

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "move", "first-task", "In Progress");

        // Assert
        result.StdOut.Should().Contain("Successfully moved");
        _ws.FileExists("Todo", "1-first-task.md").Should().BeFalse();
        _ws.FileExists("In Progress", "1-first-task.md").Should().BeTrue(because: "numbered item should be movable by its slug name");
    }

    [Fact]
    public async Task Move_FromRejected_AssignsNextNumber()
    {
        // Arrange
        _ws.AddItem("Rejected", "old-plan.md", "# Old Plan\n\n## Description\n\nRejected approach");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "move", "old-plan", "Todo");

        // Assert
        result.StdOut.Should().Contain("Successfully moved");
        _ws.FileExists("Rejected", "old-plan.md").Should().BeFalse();
        _ws.FileExists("Todo", "4-old-plan.md").Should().BeTrue(because: "promoted rejected item gets next safe number (max 3 + 1 = 4)");
    }

    [Fact]
    public async Task Move_SubItemToIdeas_StripsLetteredPrefix()
    {
        // Arrange — add a sub-item with a lettered prefix
        _ws.AddItem("Todo", "1a-sub-task.md", "# 1a - Sub Task\n\n## Description\n\nA sub-task");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "move", "1a", "Ideas");

        // Assert
        result.StdOut.Should().Contain("Successfully moved");
        _ws.FileExists("Todo", "1a-sub-task.md").Should().BeFalse();
        _ws.FileExists("ideas", "sub-task.md").Should().BeTrue(because: "lettered prefix '1a-' should be stripped when moving to Ideas");
    }

    [Fact]
    public async Task Move_SanitizesContentDuringTransfer()
    {
        // Arrange — add an item with Unicode characters that should be sanitized
        _ws.AddItem("Todo", "1-dirty.md", "# 1 - Dirty\n\n## Description\n\nSome text \u2014 with em dash");

        // Act
        await CliRunner.RunAsync(_build.DllPath, _ws.Root, "move", "1", "In Progress");

        // Assert
        var content = _ws.ReadFile("In Progress", "1-dirty.md");
        content.Should().NotContain("\u2014", because: "move should sanitize em dashes in the transferred content");
        content.Should().Contain("--", because: "em dash should be replaced with ASCII double-dash");
    }

    public void Dispose() => _ws.Dispose();
}
