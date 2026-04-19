using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class ListAndQueryTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly TestWorkspace _ws;

    public ListAndQueryTests(ToolBuildFixture build)
    {
        _build = build;
        _ws = new TestWorkspace();

        _ws.AddItem("Todo", "1-alpha-task.md", "# 1 - Alpha Task\n\n## Description\n\nFirst task about rendering");
        _ws.AddItem("Todo", "2-beta-task.md", "# 2 - Beta Task\n\n## Description\n\nSecond task about water");
        _ws.AddItem("In Progress", "3-gamma-task.md", "# 3 - Gamma Task\n\n## Description\n\nThird task in progress");
        _ws.AddItem("Done", "5-epsilon-task.md", "# 5 - Epsilon Task\n\n## Description\n\nCompleted task");
        _ws.AddItem("ideas", "cool-idea.md", "# Cool Idea\n\n## Description\n\nAn idea about physics");
        _ws.AddItem("Rejected", "bad-plan.md", "# Bad Plan\n\n## Description\n\nRejected approach");
    }

    [Fact]
    public async Task ListSummary_ReturnsAllItems()
    {
        // Arrange — workspace seeded in constructor with 6 items across all lanes

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "list", "--summary");
    }

    [Fact]
    public async Task ListFull_IncludesContent()
    {
        // Arrange — workspace seeded in constructor

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "list");

        // Assert
        var items = JsonHelper.DeserializeItems(result.StdOut);
        items.Should().HaveCount(6);
        items.Should().OnlyContain(i => !string.IsNullOrEmpty(i.Content), because: "full list should include Markdown body");
    }

    [Fact]
    public async Task ListByFolder_FiltersToTodo()
    {
        // Arrange — workspace seeded in constructor

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "list", "--folder", "Todo", "--summary");

        // Assert
        var items = JsonHelper.DeserializeSummaries(result.StdOut);
        items.Should().HaveCount(2, because: "only alpha-task and beta-task are in Todo");
        items.Should().OnlyContain(i => i.Status == "Todo");
    }

    [Fact]
    public async Task ListByFolder_FiltersToInProgress()
    {
        // Arrange — workspace seeded in constructor

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "list", "--folder", "InProgress", "--summary");

        // Assert
        var items = JsonHelper.DeserializeSummaries(result.StdOut);
        items.Should().ContainSingle(because: "only gamma-task is in progress")
            .Which.Slug.Should().Be("gamma-task");
    }

    [Fact]
    public async Task ListByFolder_FiltersToIdeas()
    {
        // Arrange — workspace seeded in constructor

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "list", "--folder", "Ideas", "--summary");

        // Assert
        var items = JsonHelper.DeserializeSummaries(result.StdOut);
        items.Should().ContainSingle(because: "only cool-idea is in Ideas")
            .Which.Slug.Should().Be("cool-idea");
    }

    [Fact]
    public async Task ListByFolder_FiltersToRejected()
    {
        // Arrange — workspace seeded in constructor

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "list", "--folder", "Rejected", "--summary");

        // Assert
        var items = JsonHelper.DeserializeSummaries(result.StdOut);
        items.Should().ContainSingle(because: "only bad-plan is in Rejected")
            .Which.Slug.Should().Be("bad-plan");
    }

    [Fact]
    public async Task GetById_ReturnsCorrectItem()
    {
        // Arrange — workspace seeded in constructor

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "show", "1");

        // Assert
        var item = JsonHelper.DeserializeItem(result.StdOut);
        item.Should().NotBeNull();
        item!.Slug.Should().Be("alpha-task");
        item.Status.Should().Be("Todo");
    }

    [Fact]
    public async Task GetBySlug_ReturnsMatchingItem()
    {
        // Arrange — workspace seeded in constructor

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "show", "gamma-task");

        // Assert
        var item = JsonHelper.DeserializeItem(result.StdOut);
        item.Should().NotBeNull();
        item!.Id.Should().Be("3");
        item.Status.Should().Be("In Progress");
    }

    [Fact]
    public async Task Search_ReturnsRankedResults()
    {
        // Arrange — workspace seeded in constructor

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "search", "alpha");

        // Assert
        var items = JsonHelper.DeserializeItems(result.StdOut);
        items.Should().NotBeEmpty();
        items[0].Slug.Should().Be("alpha-task", because: "exact slug match should rank highest");
    }

    [Fact]
    public async Task Search_FullText_FindsContentMatch()
    {
        // Arrange — workspace seeded in constructor; beta-task body contains 'water'

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "search", "water", "--full");

        // Assert
        var items = JsonHelper.DeserializeItems(result.StdOut);
        items.Should().NotBeEmpty();
        items.Should().Contain(i => i.Slug == "beta-task", because: "beta-task body mentions 'water'");
    }

    [Fact]
    public async Task Next_ReturnsHighestPriorityTodo()
    {
        // Arrange — workspace seeded in constructor; item 1 is lowest-numbered Todo

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "next");

        // Assert
        var item = JsonHelper.DeserializeItem(result.StdOut);
        item.Should().NotBeNull();
        item!.Id.Should().Be("1");
        item.Slug.Should().Be("alpha-task");
    }

    [Fact]
    public async Task NextId_ReturnsMaxPlusOne()
    {
        // Arrange — workspace seeded in constructor; highest numbered item is 5 (epsilon-task in Done)

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "next-id");

        // Assert
        result.StdOut.Trim().Should().Be("6", because: "max existing number is 5, so next safe ID is 6");
    }

    [Fact]
    public async Task TodoItemsAreOrderedByNumberAscending()
    {
        // Arrange — workspace seeded in constructor

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "list", "--folder", "Todo", "--summary");

        // Assert
        var items = JsonHelper.DeserializeSummaries(result.StdOut);
        items.Select(i => i.Id).Should().ContainInOrder("1", "2");
    }

    [Fact]
    public async Task DoneItemsAreOrderedByNumberDescending()
    {
        // Arrange
        _ws.AddItem("Done", "4-delta-task.md", "# 4 - Delta Task\n\n## Description\n\nAnother done");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "list", "--folder", "Done", "--summary");

        // Assert
        var items = JsonHelper.DeserializeSummaries(result.StdOut);
        items.Select(i => i.Id).Should().ContainInOrder("5", "4");
    }

    [Fact]
    public async Task ListByFolder_FiltersToTesting()
    {
        // Arrange
        _ws.AddItem("Testing", "4-delta-task.md", "# 4 - Delta Task\n\n## Description\n\nTesting task");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "list", "--folder", "Testing", "--summary");

        // Assert
        var items = JsonHelper.DeserializeSummaries(result.StdOut);
        items.Should().ContainSingle(because: "only delta-task is in Testing")
            .Which.Slug.Should().Be("delta-task");
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNull()
    {
        // Arrange — workspace seeded in constructor; no item with ID 999

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "show", "999");

        // Assert
        result.StdOut.Trim().Should().Be("null", because: "non-existent ID should serialize as null JSON");
    }

    [Fact]
    public async Task Search_NoMatches_ReturnsEmptyArray()
    {
        // Arrange — workspace seeded in constructor

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "search", "zzzznonexistent");

        // Assert
        var items = JsonHelper.DeserializeItems(result.StdOut);
        items.Should().BeEmpty(because: "no item matches the nonsensical search term");
    }

    [Fact]
    public async Task Search_MultiWord_ReturnsRankedResults()
    {
        // Arrange — workspace seeded in constructor; alpha-task body mentions 'rendering'

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "search", "alpha rendering", "--full");

        // Assert
        var items = JsonHelper.DeserializeItems(result.StdOut);
        items.Should().NotBeEmpty();
        items[0].Slug.Should().Be("alpha-task", because: "multi-word query should match slug + body content");
    }

    [Fact]
    public async Task GetBySlug_NonExistent_ReturnsNull()
    {
        // Arrange — workspace seeded in constructor; no item with slug 'nonexistent'

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "show", "zzzznonexistent");

        // Assert
        result.StdOut.Trim().Should().Be("null", because: "non-existent slug should serialize as null JSON");
    }

    [Fact]
    public async Task ListByFolder_InProgressWithSpace_Works()
    {
        // Arrange — workspace seeded in constructor

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "list", "--folder", "In Progress", "--summary");

        // Assert
        var items = JsonHelper.DeserializeSummaries(result.StdOut);
        items.Should().ContainSingle(because: "only gamma-task is in progress")
            .Which.Slug.Should().Be("gamma-task");
    }

    [Fact]
    public async Task Next_EmptyTodo_ReturnsNull()
    {
        // Arrange — workspace with no Todo items
        var emptyWs = new TestWorkspace();
        try
        {
            emptyWs.AddItem("Done", "1-done-task.md", "# 1 - Done Task\n\n## Description\n\nDone");

            // Act
            var result = await CliRunner.RunAsync(_build.DllPath, emptyWs.Root, "next");

            // Assert
            result.StdOut.Trim().Should().BeEmpty(because: "no items in Todo means nothing to return to stdout; the signal is empty output");
        }
        finally
        {
            emptyWs.Dispose();
        }
    }

    [Fact]
    public async Task SubItemsOrderBetweenParents()
    {
        // Arrange — items with sub-items should sort: 1, 1a, 1b, 2
        var ws2 = new TestWorkspace();
        ws2.AddItem("Todo", "1-parent.md", "# 1 - Parent\n\n## Description\n\nFirst");
        ws2.AddItem("Todo", "1a-sub-one.md", "# 1a - Sub One\n\n## Description\n\nSub A");
        ws2.AddItem("Todo", "1b-sub-two.md", "# 1b - Sub Two\n\n## Description\n\nSub B");
        ws2.AddItem("Todo", "2-second.md", "# 2 - Second\n\n## Description\n\nSecond");

        try
        {
            // Act
            var result = await CliRunner.RunAsync(_build.DllPath, ws2.Root, "list", "--folder", "Todo", "--summary");

            // Assert
            var items = JsonHelper.DeserializeSummaries(result.StdOut);
            var ids = items.Select(i => i.Id).ToList();
            ids.Should().ContainInOrder(new[] { "1", "1a", "1b", "2" },
                because: "sub-items should sort after their parent and before the next primary item");
        }
        finally
        {
            ws2.Dispose();
        }
    }

    public void Dispose() => _ws.Dispose();
}
