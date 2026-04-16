using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class ProgressTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly TestWorkspace _ws;

    public ProgressTests(ToolBuildFixture build)
    {
        _build = build;
        _ws = new TestWorkspace();
        _ws.AddItem("Todo", "1-my-task.md", "# 1 - My Task\n\n## Description\n\nA task to progress");
    }

    [Fact]
    public async Task Progress_FullWorkflow_ReadyToDone()
    {
        // Arrange — item starts in Todo (the ready lane)

        // Act — first progress: Todo -> In Progress
        var r1 = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "progress", "1");

        // Assert
        r1.StdOut.Should().Contain("In Progress", because: "first progress should move item to In Progress");
        _ws.FileExists("Todo", "1-my-task.md").Should().BeFalse(because: "item should leave Todo");
        _ws.FileExists("In Progress", "1-my-task.md").Should().BeTrue(because: "item should be in In Progress");

        // Act — second progress: In Progress -> Testing
        var r2 = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "progress", "1");

        // Assert
        r2.StdOut.Should().Contain("Testing", because: "second progress should move item to Testing");
        _ws.FileExists("In Progress", "1-my-task.md").Should().BeFalse();
        _ws.FileExists("Testing", "1-my-task.md").Should().BeTrue();

        // Act — third progress: Testing -> Done
        var r3 = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "progress", "1");

        // Assert
        r3.StdOut.Should().Contain("Done", because: "third progress should move item to Done");
        _ws.FileExists("Testing", "1-my-task.md").Should().BeFalse();
        _ws.FileExists("Done", "1-my-task.md").Should().BeTrue();

        // Act — progress when already at Done
        var r4 = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "progress", "1");

        // Assert
        r4.StdOut.Should().Contain("already in the done lane", because: "item at Done should give an informational message, not error");
        r4.StdErr.Should().BeEmpty(because: "reaching Done is not an error condition");
        _ws.FileExists("Done", "1-my-task.md").Should().BeTrue(because: "item should remain in Done");
    }

    [Fact]
    public async Task Progress_BySlug_AdvancesItem()
    {
        // Arrange — item starts in Todo; address it by slug

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "progress", "my-task");

        // Assert
        result.StdErr.Should().BeEmpty();
        result.StdOut.Should().Contain("In Progress", because: "progress by slug should advance the item");
        _ws.FileExists("In Progress", "1-my-task.md").Should().BeTrue(because: "item should be in In Progress after progress by slug");
    }

    [Fact]
    public async Task Progress_DryRun_ShowsPreviewAndMakesNoChanges()
    {
        // Arrange — item in Todo

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "progress", "1", "--dry-run");

        // Assert
        result.StdErr.Should().BeEmpty();
        result.StdOut.Should().Contain("Would move", because: "dry-run should preview the lane transition");
        result.StdOut.Should().Contain("In Progress", because: "preview should name the target lane");
        _ws.FileExists("Todo", "1-my-task.md").Should().BeTrue(because: "dry-run must not move the file");
        _ws.FileExists("In Progress", "1-my-task.md").Should().BeFalse(because: "dry-run must not create the file in the target lane");
    }

    [Fact]
    public async Task Progress_NonExistentItem_ReportsError()
    {
        // Arrange — no item with ID 999

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "progress", "999");

        // Assert
        result.StdErr.Should().Contain("Error").And.Contain("not found",
            because: "progress on a missing item should give a clear error");
    }

    public void Dispose() => _ws.Dispose();
}
