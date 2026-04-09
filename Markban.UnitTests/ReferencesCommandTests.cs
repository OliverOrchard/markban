using AwesomeAssertions;
using Xunit;

namespace Markban.UnitTests;

public class ReferencesCommandTests
{
    // ── ReferencesCommand.Execute ─────────────────────────
    [Fact]
    public void Execute_FindsReferencesToSlug()
    {
        // Arrange
        var root = CreateWorkspace();
        File.WriteAllText(
            Path.Combine(root, "Todo", "1-water-physics.md"),
            "# Water Physics\n\nDone");
        File.WriteAllText(
            Path.Combine(root, "Todo", "2-depends-on-water.md"),
            "Depends on: [water-physics]");
        File.WriteAllText(
            Path.Combine(root, "Done", "3-unrelated.md"),
            "# Unrelated\n\nNothing here");
        var items = WorkItemStore.LoadAll(root);

        // Act
        var refs = ReferencesCommand.Execute(root, items, "water-physics");

        // Assert
        refs.Should().ContainSingle(because: "only 2-depends-on-water.md references [water-physics]");
        refs[0].Slug.Should().Be("depends-on-water");
        refs[0].Line.Should().Be(1);
    }

    [Fact]
    public void Execute_ReturnsEmpty_WhenNoReferences()
    {
        // Arrange
        var root = CreateWorkspace();
        File.WriteAllText(
            Path.Combine(root, "Todo", "1-alpha.md"),
            "# Alpha\n\nNo links here");
        File.WriteAllText(
            Path.Combine(root, "Done", "2-beta.md"),
            "# Beta\n\nAlso no links");
        var items = WorkItemStore.LoadAll(root);

        // Act
        var refs = ReferencesCommand.Execute(root, items, "alpha");

        // Assert
        refs.Should().BeEmpty(because: "no file contains [alpha]");
    }

    [Fact]
    public void Execute_FindsMultipleReferences()
    {
        // Arrange
        var root = CreateWorkspace();
        File.WriteAllText(
            Path.Combine(root, "Todo", "1-target.md"),
            "# Target");
        File.WriteAllText(
            Path.Combine(root, "Todo", "2-first.md"),
            "See [target] for details");
        File.WriteAllText(
            Path.Combine(root, "Done", "3-second.md"),
            "Depends on: [target]");
        File.WriteAllText(
            Path.Combine(root, "Testing", "4-third.md"),
            "Related: [target]");
        var items = WorkItemStore.LoadAll(root);

        // Act
        var refs = ReferencesCommand.Execute(root, items, "target");

        // Assert
        refs.Should().HaveCount(3, because: "three files reference [target]");
    }

    [Fact]
    public void Execute_ExcludesIdeasByDefault()
    {
        // Arrange
        var root = CreateWorkspace();
        File.WriteAllText(
            Path.Combine(root, "Todo", "1-feature.md"),
            "# Feature");
        File.WriteAllText(
            Path.Combine(root, "ideas", "cool-idea.md"),
            "Depends on: [feature]");
        var items = WorkItemStore.LoadAll(root);

        // Act
        var without = ReferencesCommand.Execute(root, items, "feature", includeIdeas: false);
        var with = ReferencesCommand.Execute(root, items, "feature", includeIdeas: true);

        // Assert
        without.Should().BeEmpty(because: "ideas are excluded by default");
        with.Should().ContainSingle(because: "with includeIdeas, ideas/ is scanned");
    }

    [Fact]
    public void Execute_IsCaseInsensitive()
    {
        // Arrange
        var root = CreateWorkspace();
        File.WriteAllText(
            Path.Combine(root, "Todo", "1-my-feature.md"),
            "# My Feature");
        File.WriteAllText(
            Path.Combine(root, "Todo", "2-other.md"),
            "See [My-Feature] for detail");
        var items = WorkItemStore.LoadAll(root);

        // Act
        var refs = ReferencesCommand.Execute(root, items, "my-feature");

        // Assert
        refs.Should().ContainSingle(because: "[My-Feature] should match case-insensitively");
    }

    [Fact]
    public void Execute_ReportsOneHitPerFile()
    {
        // Arrange
        var root = CreateWorkspace();
        File.WriteAllText(
            Path.Combine(root, "Todo", "1-target.md"),
            "# Target");
        File.WriteAllText(
            Path.Combine(root, "Todo", "2-referrer.md"),
            "First [target] and second [target]");
        var items = WorkItemStore.LoadAll(root);

        // Act
        var refs = ReferencesCommand.Execute(root, items, "target");

        // Assert
        refs.Should().ContainSingle(because: "multiple mentions in one file count as one reference");
    }

    // ── ReferencesCommand.ResolveToSlug ───────────────────

    [Fact]
    public void ResolveToSlug_MatchesById()
    {
        // Arrange
        var items = new List<WorkItem>
        {
            new("53a", "audio-options-menu", "Done", "", "53a-audio-options-menu.md", ""),
            new("12", "water-physics", "Todo", "", "12-water-physics.md", ""),
        };

        // Act
        var slug = ReferencesCommand.ResolveToSlug("53a", items);

        // Assert
        slug.Should().Be("audio-options-menu", because: "ID 53a maps to audio-options-menu");
    }

    [Fact]
    public void ResolveToSlug_MatchesBySlug()
    {
        // Arrange
        var items = new List<WorkItem>
        {
            new("12", "water-physics", "Todo", "", "12-water-physics.md", ""),
        };

        // Act
        var slug = ReferencesCommand.ResolveToSlug("water-physics", items);

        // Assert
        slug.Should().Be("water-physics", because: "exact slug match should return the slug");
    }

    [Fact]
    public void ResolveToSlug_PrefersIdOverSlug()
    {
        // Arrange — unlikely but tests priority: ID match wins
        var items = new List<WorkItem>
        {
            new("5", "five-star-review", "Todo", "", "5-five-star-review.md", ""),
            new("10", "top-5", "Done", "", "10-top-5.md", ""),
        };

        // Act
        var slug = ReferencesCommand.ResolveToSlug("5", items);

        // Assert
        slug.Should().Be("five-star-review", because: "ID match should take priority");
    }

    [Fact]
    public void ResolveToSlug_AllowsUnknownSlugWithHyphens()
    {
        // Arrange
        var items = new List<WorkItem>();

        // Act
        var slug = ReferencesCommand.ResolveToSlug("some-unknown-feature", items);

        // Assert
        slug.Should().Be("some-unknown-feature",
            because: "hyphenated strings are treated as slugs even if not found");
    }

    [Fact]
    public void ResolveToSlug_ReturnsNull_ForUnknownBareNumber()
    {
        // Arrange
        var items = new List<WorkItem>
        {
            new("1", "alpha", "Todo", "", "1-alpha.md", ""),
        };

        // Act
        var slug = ReferencesCommand.ResolveToSlug("999", items);

        // Assert
        slug.Should().BeNull(because: "999 doesn't match any ID and has no hyphens so it's not a slug");
    }

    private static string CreateWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "wi-ref-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(root, "Todo"));
        Directory.CreateDirectory(Path.Combine(root, "In Progress"));
        Directory.CreateDirectory(Path.Combine(root, "Testing"));
        Directory.CreateDirectory(Path.Combine(root, "Done"));
        Directory.CreateDirectory(Path.Combine(root, "ideas"));
        Directory.CreateDirectory(Path.Combine(root, "Rejected"));
        return root;
    }
}
