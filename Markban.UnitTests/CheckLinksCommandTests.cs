using AwesomeAssertions;
using Xunit;

namespace Markban.UnitTests;

public class CheckLinksCommandTests
{
    [Fact]
    public void Execute_FindsBrokenLinks()
    {
        // Arrange
        var root = CreateWorkspace();
        File.WriteAllText(
            Path.Combine(root, "Todo", "1-my-feature.md"),
            "Depends on: [real-slug], [nonexistent-slug]");
        File.WriteAllText(
            Path.Combine(root, "Done", "2-real-slug.md"),
            "# Real Slug");
        var items = WorkItemStore.LoadAll(root);

        // Act
        var (broken, _) = CheckLinksCommand.Execute(root, items);

        // Assert
        broken.Should().ContainSingle(because: "only [nonexistent-slug] is broken");
        broken[0].Slug.Should().Be("nonexistent-slug");
        broken[0].Line.Should().Be(1);
    }

    [Fact]
    public void Execute_ReturnsEmpty_WhenAllLinksResolve()
    {
        // Arrange
        var root = CreateWorkspace();
        File.WriteAllText(
            Path.Combine(root, "Todo", "1-alpha.md"),
            "See [beta]");
        File.WriteAllText(
            Path.Combine(root, "Done", "2-beta.md"),
            "# Beta");
        var items = WorkItemStore.LoadAll(root);

        // Act
        var (broken, _) = CheckLinksCommand.Execute(root, items);

        // Assert
        broken.Should().BeEmpty(because: "[beta] resolves to Done/2-beta.md");
    }

    [Fact]
    public void Execute_IncludesIdeasWhenFlagSet()
    {
        // Arrange
        var root = CreateWorkspace();
        File.WriteAllText(
            Path.Combine(root, "ideas", "cool-idea.md"),
            "Depends on: [missing-feature]");
        var items = WorkItemStore.LoadAll(root);

        // Act
        var (withoutIdeas, _) = CheckLinksCommand.Execute(root, items, includeIdeas: false);
        var (withIdeas, _) = CheckLinksCommand.Execute(root, items, includeIdeas: true);

        // Assert
        withoutIdeas.Should().BeEmpty(because: "ideas are excluded by default");
        withIdeas.Should().ContainSingle(because: "[missing-feature] is broken in ideas/");
    }

    [Fact]
    public void Execute_ResolvesIdeasSlugsAsTargets()
    {
        // Arrange
        var root = CreateWorkspace();
        File.WriteAllText(
            Path.Combine(root, "Todo", "1-my-task.md"),
            "See also: [cool-idea]");
        File.WriteAllText(
            Path.Combine(root, "ideas", "cool-idea.md"),
            "# Cool Idea");
        var items = WorkItemStore.LoadAll(root);

        // Act
        var (broken, _) = CheckLinksCommand.Execute(root, items);

        // Assert
        broken.Should().BeEmpty(because: "[cool-idea] resolves to ideas/cool-idea.md");
    }

    [Fact]
    public void Execute_BrokenLinks_IncludeSuggestions()
    {
        // Arrange
        var root = CreateWorkspace();
        File.WriteAllText(
            Path.Combine(root, "Todo", "1-water-physics.md"),
            "Depends on: [water-phsyics]"); // typo in slug
        File.WriteAllText(
            Path.Combine(root, "Done", "2-water-physics.md"),
            "# Water Physics");
        var items = WorkItemStore.LoadAll(root);

        // Act
        var (broken, _) = CheckLinksCommand.Execute(root, items);

        // Assert
        broken.Should().ContainSingle(because: "[water-phsyics] is a typo of [water-physics]");
        broken[0].Suggestions.Should().NotBeEmpty(because: "fuzzy matching should suggest the correct slug");
        broken[0].Suggestions[0].Should().Contain("water-physics",
            because: "the closest match should be the correctly-spelled slug");
    }

    [Fact]
    public void FindSuggestions_ReturnsClosestMatches()
    {
        // Arrange
        var items = new List<WorkItem>
        {
            new("10", "water-physics", "Done", "", "10-water-physics.md", ""),
            new("11", "fire-system", "Todo", "", "11-fire-system.md", ""),
            new("12", "water-rendering", "In Progress", "", "12-water-rendering.md", ""),
        };

        // Act
        var suggestions = CheckLinksCommand.FindSuggestions("water-phsyics", items);

        // Assert
        suggestions.Should().NotBeEmpty(because: "there are items with overlapping words");
        suggestions[0].Should().Contain("water-physics",
            because: "water-physics has the most word overlap with water-phsyics");
    }

    [Fact]
    public void FindSuggestions_ReturnsEmpty_WhenNoWordsMatch()
    {
        // Arrange
        var items = new List<WorkItem>
        {
            new("1", "fire-system", "Todo", "", "1-fire-system.md", ""),
            new("2", "camera-shake", "Done", "", "2-camera-shake.md", ""),
        };

        // Act
        var suggestions = CheckLinksCommand.FindSuggestions("water-physics", items);

        // Assert
        suggestions.Should().BeEmpty(because: "no items share any words with the broken slug");
    }

    [Fact]
    public void FindSuggestions_LimitsResults()
    {
        // Arrange
        var items = new List<WorkItem>
        {
            new("1", "water-a", "Todo", "", "1-water-a.md", ""),
            new("2", "water-b", "Todo", "", "2-water-b.md", ""),
            new("3", "water-c", "Todo", "", "3-water-c.md", ""),
            new("4", "water-d", "Todo", "", "4-water-d.md", ""),
            new("5", "water-e", "Todo", "", "5-water-e.md", ""),
        };

        // Act
        var suggestions = CheckLinksCommand.FindSuggestions("water-x", items, maxResults: 2);

        // Assert
        suggestions.Should().HaveCount(2, because: "maxResults limits the output to 2");
    }

    [Fact]
    public void FindSuggestions_IncludesStatusInOutput()
    {
        // Arrange
        var items = new List<WorkItem>
        {
            new("10", "water-physics", "Done", "", "10-water-physics.md", ""),
        };

        // Act
        var suggestions = CheckLinksCommand.FindSuggestions("water-phsyics", items);

        // Assert
        suggestions.Should().ContainSingle();
        suggestions[0].Should().Contain("Done", because: "the suggestion should include the item's status");
        suggestions[0].Should().Contain("10:", because: "the suggestion should include the item's ID");
    }

    [Fact]
    public void Execute_DetectsBareNumericRefs()
    {
        // Arrange
        var root = CreateWorkspace();
        File.WriteAllText(
            Path.Combine(root, "Todo", "5-my-task.md"),
            "## Depends On\n- 3 (Some Feature)");
        File.WriteAllText(
            Path.Combine(root, "Done", "3-some-feature.md"),
            "# Some Feature");
        var items = WorkItemStore.LoadAll(root);

        // Act
        var (broken, numericRefs) = CheckLinksCommand.Execute(root, items);

        // Assert
        broken.Should().BeEmpty(because: "no [slug] links are broken");
        numericRefs.Should().ContainSingle(because: "'- 3 (Some Feature)' is a bare numeric ref");
        numericRefs[0].ResolvedSlug.Should().Be("some-feature",
            because: "ID 3 resolves to some-feature");
    }

    [Fact]
    public void Execute_DetectsLeadingZeroNumericRefs()
    {
        // Arrange
        var root = CreateWorkspace();
        File.WriteAllText(
            Path.Combine(root, "Done", "12-customer-jobs.md"),
            "# Customer Jobs");
        File.WriteAllText(
            Path.Combine(root, "ideas", "my-idea.md"),
            "## Depends On\n- 012 (Customer Jobs)");
        var items = WorkItemStore.LoadAll(root);

        // Act
        var (_, numericRefs) = CheckLinksCommand.Execute(root, items, includeIdeas: true);

        // Assert
        numericRefs.Should().ContainSingle(because: "'- 012 (Customer Jobs)' has a leading-zero numeric ref");
        numericRefs[0].ResolvedSlug.Should().Be("customer-jobs",
            because: "ID 12 (from 012) resolves to customer-jobs");
    }

    [Fact]
    public void Execute_NumericRefsNotScannedInIdeas_WhenFlagNotSet()
    {
        // Arrange
        var root = CreateWorkspace();
        File.WriteAllText(
            Path.Combine(root, "ideas", "my-idea.md"),
            "## Depends On\n- 99 (Something)");
        var items = WorkItemStore.LoadAll(root);

        // Act
        var (_, numericRefs) = CheckLinksCommand.Execute(root, items, includeIdeas: false);

        // Assert
        numericRefs.Should().BeEmpty(because: "ideas are excluded when includeIdeas is false");
    }

    private static string CreateWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "wi-checklinks-" + Guid.NewGuid().ToString("N")[..8]);
        foreach (var folder in new[] { "Todo", "In Progress", "Testing", "Done", "ideas", "Rejected" })
        {
            Directory.CreateDirectory(Path.Combine(root, folder));
        }

        return root;
    }
}
