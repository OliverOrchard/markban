using Markban.IntegrationTests.Infrastructure;
using AwesomeAssertions;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class CreateTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly TestWorkspace _ws;

    public CreateTests(ToolBuildFixture build)
    {
        _build = build;
        _ws = new TestWorkspace();

        _ws.AddItem("Todo", "1-alpha-task.md", "# 1 - Alpha Task\n\n## Description\n\nFirst task");
        _ws.AddItem("Todo", "2-beta-task.md", "# 2 - Beta Task\n\n## Description\n\nSecond task");
        _ws.AddItem("Done", "3-gamma-task.md", "# 3 - Gamma Task\n\n## Description\n\nDone task");
    }

    [Fact]
    public async Task Create_AppendsToTodoByDefault()
    {
        // Arrange — workspace seeded in constructor with items 1-3

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "create", "New Feature");

        // Assert
        result.StdOut.Should().Contain("Successfully created");
        _ws.FileExists("Todo", "4-new-feature.md").Should().BeTrue(because: "next available number after max(3) is 4");

        var content = _ws.ReadFile("Todo", "4-new-feature.md");
        content.Should().Contain("# 4 - New Feature");
    }

    [Fact]
    public async Task Create_InSpecificLane()
    {
        // Arrange — workspace seeded in constructor

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "create", "Testing Item", "--lane", "Testing");

        // Assert
        result.StdOut.Should().Contain("Successfully created");
        _ws.FileExists("Testing", "4-testing-item.md").Should().BeTrue(because: "item should land in the Testing lane");
    }

    [Fact]
    public async Task Create_WithPriority_InsertsAtTop()
    {
        // Arrange — workspace seeded in constructor with items 1 and 2 in Todo

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "create", "Urgent Fix", "--priority");

        // Assert
        result.StdOut.Should().Contain("Successfully created");
        _ws.FileExists("Todo", "1-urgent-fix.md").Should().BeTrue(because: "--priority inserts at position 1");
        _ws.FileExists("Todo", "2-alpha-task.md").Should().BeTrue(because: "former item 1 should shift to 2");
        _ws.FileExists("Todo", "3-beta-task.md").Should().BeTrue(because: "former item 2 should shift to 3");
    }

    [Fact]
    public async Task Create_AfterSpecificItem()
    {
        // Arrange — workspace seeded in constructor with items 1 and 2 in Todo

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "create", "Inserted Task", "--after", "1");

        // Assert
        result.StdOut.Should().Contain("Successfully created");
        _ws.FileExists("Todo", "2-inserted-task.md").Should().BeTrue(because: "--after 1 inserts at position 2");
        _ws.FileExists("Todo", "3-beta-task.md").Should().BeTrue(because: "former item 2 should shift to 3");
    }

    [Fact]
    public async Task Create_InIdeasLane_HasNoNumberPrefix()
    {
        // Arrange — workspace seeded in constructor

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "create", "Maybe Later", "--lane", "Ideas");

        // Assert
        result.StdOut.Should().Contain("Successfully created");
        _ws.FileExists("ideas", "maybe-later.md").Should().BeTrue(because: "Ideas items are slug-only with no number prefix");
    }

    [Fact]
    public async Task Create_SubItem_AppendsNextLetter()
    {
        // Arrange — workspace seeded in constructor; parent item 1 has no existing sub-items

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "create", "Sub Work", "--sub-item", "--parent", "1");

        // Assert
        result.StdOut.Should().Contain("Successfully created sub-item");
        _ws.FileExists("Todo", "1a-sub-work.md").Should().BeTrue(because: "first sub-item of parent 1 gets letter 'a'");
    }

    [Fact]
    public async Task Create_SubItem_AfterExisting_ShiftsLetters()
    {
        // Arrange
        await CliRunner.RunAsync(_build.DllPath, _ws.Root, "create", "Sub A", "--sub-item", "--parent", "1");
        _ws.FileExists("Todo", "1a-sub-a.md").Should().BeTrue();

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "create", "Sub Between", "--sub-item", "--parent", "1", "--after", "1a");

        // Assert
        result.StdOut.Should().Contain("Successfully created sub-item");
        _ws.FileExists("Todo", "1b-sub-between.md").Should().BeTrue(because: "--after 1a inserts at letter 'b'");
        _ws.FileExists("Todo", "1a-sub-a.md").Should().BeTrue(because: "original 1a is unchanged when nothing exists at 'b'+");
    }

    [Fact]
    public async Task Create_DuplicateSlug_ReportsError()
    {
        // Arrange — workspace seeded in constructor with 'alpha-task' already present

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "create", "Alpha Task");

        // Assert
        result.StdOut.Should().Contain("Error").And.Contain("already exists");
    }

    [Fact]
    public async Task Create_GeneratesBoilerplateContent()
    {
        // Arrange — workspace seeded in constructor

        // Act
        await CliRunner.RunAsync(_build.DllPath, _ws.Root, "create", "Boilerplate Check");

        // Assert
        var content = _ws.ReadFile("Todo", "4-boilerplate-check.md");
        content.Should().NotContain("## Priority");
        content.Should().Contain("## Description");
        content.Should().Contain("## Acceptance Criteria");
    }

    [Fact]
    public async Task Create_InRejectedLane_HasNoNumberPrefix()
    {
        // Arrange — workspace seeded in constructor

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "create", "Bad Approach", "--lane", "Rejected");

        // Assert
        result.StdOut.Should().Contain("Successfully created");
        _ws.FileExists("Rejected", "bad-approach.md").Should().BeTrue(because: "Rejected items are slug-only with no number prefix");
    }

    [Fact]
    public async Task Create_SubItem_WithoutParent_ReportsError()
    {
        // Arrange — workspace seeded in constructor

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "create", "Orphan Sub", "--sub-item");

        // Assert
        result.StdOut.Should().Contain("Error").And.Contain("--parent");
    }

    [Fact]
    public async Task Create_SubItem_InSpecificLane()
    {
        // Arrange
        _ws.AddItem("In Progress", "3-gamma-task.md", "# 3 - Gamma Task\n\n## Description\n\nIn progress");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "create", "Sub Gamma", "--sub-item", "--parent", "3", "--lane", "In Progress");

        // Assert
        result.StdOut.Should().Contain("Successfully created sub-item");
        _ws.FileExists("In Progress", "3a-sub-gamma.md").Should().BeTrue(because: "sub-item should be created in the specified lane");
    }

    [Fact]
    public async Task Create_AfterNonExistentId_ReportsError()
    {
        // Arrange — workspace seeded in constructor; no item with ID 99 in Todo

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "create", "Orphan Insert", "--after", "99");

        // Assert
        result.StdOut.Should().Contain("Error").And.Contain("not found", because: "--after with a non-existent ID should report an error");
    }

    public void Dispose() => _ws.Dispose();
}
