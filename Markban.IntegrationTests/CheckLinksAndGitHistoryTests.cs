using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class CheckLinksAndGitHistoryTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly TestWorkspace _ws;

    public CheckLinksAndGitHistoryTests(ToolBuildFixture build)
    {
        _build = build;
        _ws = new TestWorkspace();
    }

    [Fact]
    public async Task CheckLinks_NoBrokenLinks_PrintsCleanMessage()
    {
        // Arrange
        _ws.AddItem("Todo", "1-alpha.md", "# 1 - Alpha\n\nSee [beta]");
        _ws.AddItem("Done", "2-beta.md", "# 2 - Beta\n\nDone");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "health", "check-links");

        // Assert
        result.StdOut.Should().Contain("No broken links found",
            because: "[beta] resolves to 2-beta.md");
    }

    [Fact]
    public async Task CheckLinks_BrokenLink_ShowsSuggestions()
    {
        // Arrange
        _ws.AddItem("Todo", "1-water-physics.md", "# 1 - Water Physics\n\nDone");
        _ws.AddItem("Todo", "2-depends.md", "# 2 - Depends\n\nNeeds [water-phsyics]");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "health", "check-links");

        // Assert
        result.StdOut.Should().Contain("[water-phsyics]",
            because: "it should report the broken slug");
        result.StdOut.Should().Contain("Did you mean",
            because: "fuzzy matching should suggest water-physics");
        result.StdOut.Should().Contain("water-physics",
            because: "water-physics is the closest match");
    }

    [Fact]
    public async Task CheckLinks_BrokenLink_NoMatches_PrintsNoMatchMessage()
    {
        // Arrange
        _ws.AddItem("Todo", "1-alpha.md", "# 1 - Alpha\n\nSee [completely-unrelated-thing]");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "health", "check-links");

        // Assert
        result.StdOut.Should().Contain("No potential matches found",
            because: "there are no similar slugs in the workspace");
    }

    [Fact]
    public async Task CheckLinks_IncludeIdeas_ScansIdeasFolder()
    {
        // Arrange
        _ws.AddItem("ideas", "cool-idea.md", "# Cool Idea\n\nSee [missing-feature]");

        // Act
        var without = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "health", "check-links");
        var with = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "health", "check-links", "--include-ideas");

        // Assert
        without.StdOut.Should().Contain("No broken links found",
            because: "ideas are excluded by default");
        with.StdOut.Should().Contain("[missing-feature]",
            because: "with --include-ideas, the broken link in ideas/ should be found");
    }

    [Fact]
    public async Task CheckLinks_DetectsBareNumericRefs_ListStyle()
    {
        // Arrange
        _ws.AddItem("Todo", "1-alpha.md", "# 1 - Alpha\n\nDone");
        _ws.AddItem("Todo", "2-beta.md", "# 2 - Beta\n\n## Depends On\n- 1 (Alpha)");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "health", "check-links");

        // Assert
        result.StdOut.Should().Contain("bare numeric reference",
            because: "'- 1 (Alpha)' is a bare numeric ref that should use [slug] format");
        result.StdOut.Should().Contain("Convert to:",
            because: "ID 1 should resolve to [alpha]");
    }

    [Fact]
    public async Task CheckLinks_DetectsBareNumericRefs_InlineStyle()
    {
        // Arrange
        _ws.AddItem("Done", "10-feature.md", "# 10 - Feature\n\nDone");
        _ws.AddItem("Todo", "11-task.md", "# 11 - Task\n\nSee feature (10) for details");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "health", "check-links");

        // Assert
        result.StdOut.Should().Contain("bare numeric reference",
            because: "'(10)' is an inline numeric ref");
        result.StdOut.Should().Contain("Convert to:",
            because: "ID 10 should resolve to [feature]");
    }

    [Fact]
    public async Task GitHistory_NoFileArg_PrintsError()
    {
        // Arrange — no file path provided

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "git-history");

        // Assert
        result.StdErr.Should().Contain("requires a file path",
            because: "--git-history needs a file path argument");
    }

    // ── --references ──────────────────────────────────────

    [Fact]
    public async Task References_FindsReferencingItems()
    {
        // Arrange
        _ws.AddItem("Todo", "1-water-physics.md", "# 1 - Water Physics\n\nDone");
        _ws.AddItem("Todo", "2-depends.md", "# 2 - Depends\n\nNeeds [water-physics]");
        _ws.AddItem("Done", "3-unrelated.md", "# 3 - Unrelated\n\nNo refs");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "references", "water-physics");

        // Assert
        result.StdOut.Should().Contain("1 reference(s)",
            because: "only 2-depends.md references [water-physics]");
        result.StdOut.Should().Contain("depends",
            because: "the referencing item's slug should appear in output");
    }

    [Fact]
    public async Task References_NoReferences_PrintsCleanMessage()
    {
        // Arrange
        _ws.AddItem("Todo", "1-alpha.md", "# 1 - Alpha\n\nNo links");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "references", "alpha");

        // Assert
        result.StdOut.Should().Contain("No references to [alpha] found",
            because: "nothing references [alpha]");
    }

    [Fact]
    public async Task References_IncludeIdeas_ScansIdeasFolder()
    {
        // Arrange
        _ws.AddItem("Todo", "1-feature.md", "# 1 - Feature\n\nDone");
        _ws.AddItem("ideas", "cool-idea.md", "# Cool Idea\n\nDepends on: [feature]");

        // Act
        var without = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "references", "feature");
        var with = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "references", "feature", "--include-ideas");

        // Assert
        without.StdOut.Should().Contain("No references",
            because: "ideas are excluded by default");
        with.StdOut.Should().Contain("1 reference(s)",
            because: "with --include-ideas, the reference in ideas/ should be found");
    }

    [Fact]
    public async Task References_NoSlugArg_PrintsError()
    {
        // Arrange — no slug provided

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "references");

        // Assert
        result.StdErr.Should().Contain("requires a slug",
            because: "--references needs a slug argument");
    }

    [Fact]
    public async Task References_AcceptsIdAndResolvesToSlug()
    {
        // Arrange
        _ws.AddItem("Todo", "1-water-physics.md", "# 1 - Water Physics\n\nDone");
        _ws.AddItem("Todo", "2-depends.md", "# 2 - Depends\n\nNeeds [water-physics]");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "references", "1");

        // Assert
        result.StdOut.Should().Contain("1 reference(s)",
            because: "ID '1' should resolve to slug 'water-physics' and find the reference");
        result.StdOut.Should().Contain("depends",
            because: "2-depends.md references [water-physics]");
    }

    [Fact]
    public async Task References_UnknownBareId_PrintsError()
    {
        // Arrange
        _ws.AddItem("Todo", "1-alpha.md", "# 1 - Alpha\n\nNo links");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "references", "999");

        // Assert
        result.StdErr.Should().Contain("could not resolve",
            because: "ID 999 doesn't match any work item and isn't a slug");
    }

    public void Dispose() => _ws.Dispose();
}
