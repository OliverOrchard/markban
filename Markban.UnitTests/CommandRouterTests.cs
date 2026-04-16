using AwesomeAssertions;
using Xunit;

namespace Markban.UnitTests;

public class CommandRouterTests
{
    [Fact]
    public void Routes_ContainsAllNineteenRoutes_AfterInit()
    {
        CommandRouter.Routes.Should().HaveCount(19,
            because: "there are nineteen CLI commands routed through the strategy pattern");
    }

    [Fact]
    public void Routes_AreInExpectedOrder()
    {
        // Arrange
        var expectedTypes = new[]
        {
            typeof(WebRoute),
            typeof(HelpRoute),
            typeof(InitRoute),
            typeof(ListRoute),
            typeof(NextRoute),
            typeof(ShowRoute),
            typeof(SearchRoute),
            typeof(MoveRoute),
            typeof(ProgressRoute),
            typeof(NextIdRoute),
            typeof(ReorderRoute),
            typeof(CreateRoute),
            typeof(RenameRoute),
            typeof(OverviewRoute),
            typeof(SanitizeRoute),
            typeof(HealthRoute),
            typeof(ReferencesRoute),
            typeof(GitHistoryRoute),
            typeof(CommitRoute),
        };

        // Act
        var actualTypes = CommandRouter.Routes.Select(r => r.GetType()).ToArray();

        // Assert
        actualTypes.Should().Equal(expectedTypes,
            because: "route evaluation order matters — help should be checked first");
    }

    [Fact]
    public void AllRoutes_HaveNonEmptyHelp()
    {
        // Arrange / Act / Assert
        foreach (var route in CommandRouter.Routes)
        {
            route.Help.Usage.Should().NotBeNullOrWhiteSpace(
                because: $"{route.GetType().Name} must declare a Usage string");
            route.Help.Description.Should().NotBeNullOrWhiteSpace(
                because: $"{route.GetType().Name} must declare a Description");
        }
    }

    [Fact]
    public void Route_ReturnsFalse_ForUnknownArgs()
    {
        // Arrange
        var tempDir = CreateTempWorkspace();

        try
        {
            // Act
            var result = CommandRouter.Route(["--nonexistent"], tempDir);

            // Assert
            result.Should().BeFalse(because: "no route should match an unknown argument");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void Route_ReturnsTrue_ForEmptyArgs_RoutesToHelp()
    {
        // Arrange
        var tempDir = CreateTempWorkspace();

        try
        {
            // Act
            var result = CommandRouter.Route([], tempDir);

            // Assert
            result.Should().BeTrue(because: "empty args should route to help");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

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
}
