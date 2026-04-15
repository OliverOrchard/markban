using AwesomeAssertions;
using Xunit;

namespace Markban.UnitTests;

public class RouteMatchingTests
{
    [Theory]
    [InlineData("help")]
    [InlineData("--help")]
    [InlineData("-h")]
    public void HelpRoute_Matches(string arg)
    {
        new HelpRoute().TryRoute([arg], DummyRoot).Should().BeTrue(
            because: $"HelpRoute should match '{arg}'");
    }

    [Fact]
    public void HelpRoute_Matches_EmptyArgs()
    {
        new HelpRoute().TryRoute([], DummyRoot).Should().BeTrue(
            because: "HelpRoute should match empty args");
    }

    [Fact]
    public void HelpRoute_DoesNotMatch_UnrelatedArg()
    {
        new HelpRoute().TryRoute(["list"], DummyRoot).Should().BeFalse(
            because: "HelpRoute should not match 'list' subcommand");
    }

    [Fact]
    public void ListRoute_Matches()
    {
        new ListRoute().TryRoute(["list"], TempRoot).Should().BeTrue(
            because: "ListRoute should match 'list' subcommand");
    }

    [Fact]
    public void ListRoute_Matches_WithFlags()
    {
        new ListRoute().TryRoute(["list", "--folder", "Todo", "--summary"], TempRoot).Should().BeTrue(
            because: "ListRoute should match 'list' with folder and summary flags");
    }

    [Fact]
    public void ListRoute_DoesNotMatch_UnrelatedArg()
    {
        new ListRoute().TryRoute(["move", "1", "Done"], TempRoot).Should().BeFalse(
            because: "ListRoute should not match 'move' subcommand");
    }

    [Fact]
    public void NextRoute_Matches()
    {
        new NextRoute().TryRoute(["next"], TempRoot).Should().BeTrue(
            because: "NextRoute should match 'next' subcommand");
    }

    [Fact]
    public void NextRoute_DoesNotMatch_UnrelatedArg()
    {
        new NextRoute().TryRoute(["list"], TempRoot).Should().BeFalse(
            because: "NextRoute should not match 'list'");
    }

    [Fact]
    public void ShowRoute_Matches()
    {
        new ShowRoute().TryRoute(["show", "1"], TempRoot).Should().BeTrue(
            because: "ShowRoute should match 'show <id>'");
    }

    [Fact]
    public void ShowRoute_Matches_WithSlug()
    {
        new ShowRoute().TryRoute(["show", "some-slug"], TempRoot).Should().BeTrue(
            because: "ShowRoute should match 'show <slug>'");
    }

    [Fact]
    public void ShowRoute_DoesNotMatch_UnrelatedArg()
    {
        new ShowRoute().TryRoute(["list"], DummyRoot).Should().BeFalse(
            because: "ShowRoute should not match 'list'");
    }

    [Fact]
    public void ShowRoute_MatchesAndPrintsError_WhenNoIdentifier()
    {
        new ShowRoute().TryRoute(["show"], TempRoot).Should().BeTrue(
            because: "ShowRoute should match 'show' even without an identifier (prints error)");
    }

    [Fact]
    public void SearchRoute_Matches()
    {
        new SearchRoute().TryRoute(["search", "foo"], TempRoot).Should().BeTrue(
            because: "SearchRoute should match 'search <term>'");
    }

    [Fact]
    public void SearchRoute_DoesNotMatch_UnrelatedArg()
    {
        new SearchRoute().TryRoute(["list"], DummyRoot).Should().BeFalse(
            because: "SearchRoute should not match 'list'");
    }

    [Fact]
    public void NextIdRoute_Matches()
    {
        new NextIdRoute().TryRoute(["next-id"], TempRoot).Should().BeTrue(
            because: "NextIdRoute should match 'next-id' subcommand");
    }

    [Fact]
    public void NextIdRoute_DoesNotMatch_UnrelatedArg()
    {
        new NextIdRoute().TryRoute(["list"], TempRoot).Should().BeFalse(
            because: "NextIdRoute should not match 'list'");
    }

    [Fact]
    public void OverviewRoute_Matches()
    {
        new OverviewRoute().TryRoute(["overview"], TempRoot).Should().BeTrue(
            because: "OverviewRoute should match 'overview' subcommand");
    }

    [Fact]
    public void OverviewRoute_DoesNotMatch_UnrelatedArg()
    {
        new OverviewRoute().TryRoute(["list"], TempRoot).Should().BeFalse(
            because: "OverviewRoute should not match 'list'");
    }

    [Fact]
    public void SanitizeRoute_Matches()
    {
        new SanitizeRoute().TryRoute(["sanitize"], TempRoot).Should().BeTrue(
            because: "SanitizeRoute should match 'sanitize' subcommand");
    }

    [Fact]
    public void SanitizeRoute_DoesNotMatch_UnrelatedArg()
    {
        new SanitizeRoute().TryRoute(["list"], TempRoot).Should().BeFalse(
            because: "SanitizeRoute should not match 'list'");
    }

    [Fact]
    public void MoveRoute_DoesNotMatch_UnrelatedArg()
    {
        new MoveRoute().TryRoute(["list"], TempRoot).Should().BeFalse(
            because: "MoveRoute should not match 'list'");
    }

    [Fact]
    public void CreateRoute_DoesNotMatch_UnrelatedArg()
    {
        new CreateRoute().TryRoute(["list"], TempRoot).Should().BeFalse(
            because: "CreateRoute should not match 'list'");
    }

    [Fact]
    public void ReorderRoute_DoesNotMatch_UnrelatedArg()
    {
        new ReorderRoute().TryRoute(["list"], TempRoot).Should().BeFalse(
            because: "ReorderRoute should not match 'list'");
    }

    [Fact]
    public void ReorderRoute_MatchesAndPrintsUsage_WhenTooFewArgs()
    {
        new ReorderRoute().TryRoute(["reorder"], TempRoot).Should().BeTrue(
            because: "ReorderRoute should match 'reorder' even with insufficient args (prints usage)");
    }

    [Fact]
    public void CreateRoute_MatchesAndPrintsUsage_WhenNoTitle()
    {
        new CreateRoute().TryRoute(["create"], TempRoot).Should().BeTrue(
            because: "CreateRoute should match 'create' even without a title (prints usage)");
    }

    [Fact]
    public void HealthRoute_Matches_RunAll()
    {
        new HealthRoute().TryRoute(["health"], TempRoot).Should().BeTrue(
            because: "HealthRoute should match 'health' with no subcommand");
    }

    [Fact]
    public void HealthRoute_Matches_CheckLinks()
    {
        new HealthRoute().TryRoute(["health", "check-links"], TempRoot).Should().BeTrue(
            because: "HealthRoute should match 'health check-links'");
    }

    [Fact]
    public void HealthRoute_Matches_CheckOrder()
    {
        new HealthRoute().TryRoute(["health", "check-order"], TempRoot).Should().BeTrue(
            because: "HealthRoute should match 'health check-order'");
    }

    [Fact]
    public void HealthRoute_DoesNotMatch_UnrelatedArg()
    {
        new HealthRoute().TryRoute(["list"], TempRoot).Should().BeFalse(
            because: "HealthRoute should not match 'list'");
    }

    [Fact]
    public void GitHistoryRoute_DoesNotMatch_UnrelatedArg()
    {
        new GitHistoryRoute().TryRoute(["list"], DummyRoot).Should().BeFalse(
            because: "GitHistoryRoute should not match 'list'");
    }

    [Fact]
    public void ReferencesRoute_Matches()
    {
        new ReferencesRoute().TryRoute(["references", "some-slug"], TempRoot).Should().BeTrue(
            because: "ReferencesRoute should match 'references <slug>'");
    }

    [Fact]
    public void ReferencesRoute_DoesNotMatch_UnrelatedArg()
    {
        new ReferencesRoute().TryRoute(["list"], DummyRoot).Should().BeFalse(
            because: "ReferencesRoute should not match 'list'");
    }

    [Fact]
    public void ReferencesRoute_MatchesAndPrintsError_WhenNoSlug()
    {
        new ReferencesRoute().TryRoute(["references"], TempRoot).Should().BeTrue(
            because: "ReferencesRoute should match 'references' even without a slug (prints error)");
    }

    [Fact]
    public void AllRoutes_HaveNonEmptyHelpEntry()
    {
        // Arrange — Act
        var routes = CommandRouter.Routes;

        // Assert
        routes.Should().AllSatisfy(route =>
        {
            route.Help.Usage.Should().NotBeNullOrWhiteSpace(
                because: $"{route.GetType().Name} must have a non-empty Help.Usage");
            route.Help.Description.Should().NotBeNullOrWhiteSpace(
                because: $"{route.GetType().Name} must have a non-empty Help.Description");
        });
    }

    #region Test Infrastructure

    // Dummy root that doesn't need to exist — for routes that return false immediately
    private const string DummyRoot = "C:\\nonexistent";

    // Temp workspace root — for routes that match and call through to commands
    private string? _tempRoot;
    private string TempRoot => _tempRoot ??= CreateTempWorkspace();

    private static string CreateTempWorkspace()
    {
        var root = Path.Combine(Path.GetTempPath(), "wi-unit-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(root, "Todo"));
        Directory.CreateDirectory(Path.Combine(root, "In Progress"));
        Directory.CreateDirectory(Path.Combine(root, "Testing"));
        Directory.CreateDirectory(Path.Combine(root, "Done"));
        Directory.CreateDirectory(Path.Combine(root, "ideas"));
        Directory.CreateDirectory(Path.Combine(root, "Rejected"));
        return root;
    }

    #endregion
}
