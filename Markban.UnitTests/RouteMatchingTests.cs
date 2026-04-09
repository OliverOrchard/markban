using AwesomeAssertions;
using Xunit;

namespace Markban.UnitTests;

public class RouteMatchingTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void HelpRoute_Matches(string arg)
    {
        new HelpRoute().TryRoute([arg], DummyRoot).Should().BeTrue(
            because: $"HelpRoute should match '{arg}'");
    }

    [Fact]
    public void HelpRoute_DoesNotMatch_UnrelatedArg()
    {
        new HelpRoute().TryRoute(["--list"], DummyRoot).Should().BeFalse(
            because: "HelpRoute should not match '--list'");
    }

    [Theory]
    [InlineData("--list")]
    [InlineData("-l")]
    [InlineData("--json")]
    [InlineData("--next")]
    public void ListRoute_Matches(string arg)
    {
        new ListRoute().TryRoute([arg], TempRoot).Should().BeTrue(
            because: $"ListRoute should match '{arg}'");
    }

    [Theory]
    [InlineData("--id", "1")]
    [InlineData("--slug", "test")]
    [InlineData("--search", "foo")]
    public void ListRoute_MatchesWithValue(string flag, string value)
    {
        new ListRoute().TryRoute([flag, value], TempRoot).Should().BeTrue(
            because: $"ListRoute should match '{flag} {value}'");
    }

    [Fact]
    public void ListRoute_DoesNotMatch_UnrelatedArg()
    {
        new ListRoute().TryRoute(["--move", "1", "Done"], TempRoot).Should().BeFalse(
            because: "ListRoute should not match '--move'");
    }

    [Theory]
    [InlineData("--next-id")]
    public void NextIdRoute_Matches(string arg)
    {
        new NextIdRoute().TryRoute([arg], TempRoot).Should().BeTrue(
            because: $"NextIdRoute should match '{arg}'");
    }

    [Fact]
    public void NextIdRoute_DoesNotMatch_UnrelatedArg()
    {
        new NextIdRoute().TryRoute(["--list"], TempRoot).Should().BeFalse(
            because: "NextIdRoute should not match '--list'");
    }

    [Theory]
    [InlineData("--overview")]
    public void OverviewRoute_Matches(string arg)
    {
        new OverviewRoute().TryRoute([arg], TempRoot).Should().BeTrue(
            because: $"OverviewRoute should match '{arg}'");
    }

    [Fact]
    public void OverviewRoute_DoesNotMatch_UnrelatedArg()
    {
        new OverviewRoute().TryRoute(["--list"], TempRoot).Should().BeFalse(
            because: "OverviewRoute should not match '--list'");
    }

    [Fact]
    public void SanitizeRoute_Matches()
    {
        new SanitizeRoute().TryRoute(["--sanitize"], TempRoot).Should().BeTrue(
            because: "SanitizeRoute should match '--sanitize'");
    }

    [Fact]
    public void SanitizeRoute_DoesNotMatch_UnrelatedArg()
    {
        new SanitizeRoute().TryRoute(["--list"], TempRoot).Should().BeFalse(
            because: "SanitizeRoute should not match '--list'");
    }

    [Fact]
    public void MoveRoute_DoesNotMatch_UnrelatedArg()
    {
        new MoveRoute().TryRoute(["--list"], TempRoot).Should().BeFalse(
            because: "MoveRoute should not match '--list'");
    }

    [Fact]
    public void CreateRoute_DoesNotMatch_UnrelatedArg()
    {
        new CreateRoute().TryRoute(["--list"], TempRoot).Should().BeFalse(
            because: "CreateRoute should not match '--list'");
    }

    [Fact]
    public void ReorderRoute_DoesNotMatch_UnrelatedArg()
    {
        new ReorderRoute().TryRoute(["--list"], TempRoot).Should().BeFalse(
            because: "ReorderRoute should not match '--list'");
    }

    [Fact]
    public void ReorderRoute_MatchesAndPrintsUsage_WhenTooFewArgs()
    {
        // --reorder with no folder/order args should still match (prints usage)
        new ReorderRoute().TryRoute(["--reorder"], TempRoot).Should().BeTrue(
            because: "ReorderRoute should match '--reorder' even with insufficient args (prints usage)");
    }

    [Fact]
    public void CreateRoute_MatchesAndPrintsUsage_WhenNoTitle()
    {
        // --create with no title arg should still match (prints usage)
        new CreateRoute().TryRoute(["--create"], TempRoot).Should().BeTrue(
            because: "CreateRoute should match '--create' even without a title (prints usage)");
    }

    [Fact]
    public void CheckLinksRoute_Matches()
    {
        new CheckLinksRoute().TryRoute(["--check-links"], TempRoot).Should().BeTrue(
            because: "CheckLinksRoute should match '--check-links'");
    }

    [Fact]
    public void CheckLinksRoute_DoesNotMatch_UnrelatedArg()
    {
        new CheckLinksRoute().TryRoute(["--list"], TempRoot).Should().BeFalse(
            because: "CheckLinksRoute should not match '--list'");
    }

    [Fact]
    public void GitHistoryRoute_DoesNotMatch_UnrelatedArg()
    {
        new GitHistoryRoute().TryRoute(["--list"], DummyRoot).Should().BeFalse(
            because: "GitHistoryRoute should not match '--list'");
    }

    [Fact]
    public void ReferencesRoute_Matches()
    {
        new ReferencesRoute().TryRoute(["--references", "some-slug"], TempRoot).Should().BeTrue(
            because: "ReferencesRoute should match '--references <slug>'");
    }

    [Fact]
    public void ReferencesRoute_DoesNotMatch_UnrelatedArg()
    {
        new ReferencesRoute().TryRoute(["--list"], DummyRoot).Should().BeFalse(
            because: "ReferencesRoute should not match '--list'");
    }

    [Fact]
    public void ReferencesRoute_MatchesAndPrintsError_WhenNoSlug()
    {
        new ReferencesRoute().TryRoute(["--references"], TempRoot).Should().BeTrue(
            because: "ReferencesRoute should match '--references' even without a slug (prints error)");
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
