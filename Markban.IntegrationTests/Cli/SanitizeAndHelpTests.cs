using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class SanitizeAndHelpTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly TestWorkspace _ws;

    public SanitizeAndHelpTests(ToolBuildFixture build)
    {
        _build = build;
        _ws = new TestWorkspace();
    }

    [Fact]
    public async Task Help_PrintsUsageInfo()
    {
        // Arrange — empty workspace

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "--help");

        // Assert
        result.StdOut.Should().Contain("markban - markdown board CLI");
        result.StdOut.Should().Contain("list");
        result.StdOut.Should().Contain("--root");
    }

    [Fact]
    public async Task Sanitize_FixesUnicodeCharacters()
    {
        // Arrange
        _ws.AddItem("Todo", "1-unicode-test.md", "# 1 - Unicode Test\n\nSome text \u2014 with em dash");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "sanitize");

        // Assert
        result.StdOut.Should().Contain("Sanitized");
        var content = _ws.ReadFile("Todo", "1-unicode-test.md");
        content.Should().Contain("--", because: "U+2014 em dash should be replaced with ASCII double-dash");
        content.Should().NotContain("\u2014");
    }

    [Fact]
    public async Task Sanitize_EmptyWorkspace_ReportsZeroProcessed()
    {
        // Arrange — empty workspace

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "sanitize");

        // Assert
        result.StdOut.Should().Contain("Processed 0 files");
    }

    [Fact]
    public async Task EmptyWorkspace_ListReturnsEmptyArray()
    {
        // Arrange — empty workspace

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "list", "--summary");

        // Assert
        var items = JsonHelper.DeserializeSummaries(result.StdOut);
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task Sanitize_ConvertsWiNnnRefsToSlugLinks()
    {
        // Arrange
        _ws.AddItem("Todo", "1-alpha-task.md", "# 1 - Alpha Task\n\n## Description\n\nFirst task");
        _ws.AddItem("Todo", "2-beta-task.md", "# 2 - Beta Task\n\n## Description\n\nSee WI-1 for details");

        // Act
        await CliRunner.RunAsync(_build.DllPath, _ws.Root, "sanitize");

        // Assert
        var content = _ws.ReadFile("Todo", "2-beta-task.md");
        content.Should().Contain("[alpha-task]", because: "WI-1 should resolve to [alpha-task] slug link");
        content.Should().NotContain("WI-1");
    }

    [Fact]
    public async Task Sanitize_WarnsOnUnresolvableWiRefs()
    {
        // Arrange
        _ws.AddItem("Todo", "1-alpha-task.md", "# 1 - Alpha Task\n\n## Description\n\nSee WI-99 for details");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "sanitize");

        // Assert
        result.StdOut.Should().Contain("Warning", because: "WI-99 cannot be resolved to any existing item");
        result.StdOut.Should().Contain("unresolvable", because: "output should identify the issue as unresolvable");
    }

    [Fact]
    public async Task Sanitize_FixesSmartQuotes()
    {
        // Arrange
        _ws.AddItem("Todo", "1-quotes-test.md", "# 1 - Quotes Test\n\n\u201CHello\u201D and \u2018world\u2019");

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "sanitize");

        // Assert
        result.StdOut.Should().Contain("Sanitized");
        var content = _ws.ReadFile("Todo", "1-quotes-test.md");
        content.Should().NotContain("\u201C", because: "left double smart quote should be replaced");
        content.Should().NotContain("\u201D", because: "right double smart quote should be replaced");
        content.Should().NotContain("\u2018", because: "left single smart quote should be replaced");
        content.Should().NotContain("\u2019", because: "right single smart quote should be replaced");
    }

    [Fact]
    public async Task UnknownCommand_PrintsErrorMessage()
    {
        // Arrange — empty workspace

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "--bogus");

        // Assert
        result.StdOut.Should().Contain("Unknown command");
    }

    [Theory]
    [InlineData("list")]
    [InlineData("create")]
    [InlineData("commit")]
    [InlineData("health")]
    [InlineData("search")]
    [InlineData("web")]
    [InlineData("reorder")]
    [InlineData("references")]
    public async Task SubcommandHelp_PrintsScopedHelp(string subcommand)
    {
        // Arrange — empty workspace

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, subcommand, "-h");

        // Assert
        result.StdOut.Should().Contain($"markban {subcommand}",
            because: $"'{subcommand} -h' should show scoped usage for that subcommand");
        result.StdOut.Should().NotContain("markban - markdown board CLI",
            because: "subcommand help should not dump the full help screen");
    }

    [Fact]
    public async Task UnknownFlag_OnList_PrintsScopedHelp()
    {
        // Arrange — empty workspace

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "list", "-folder", "Todo");

        // Assert
        result.StdErr.Should().Contain("Unknown flag '-folder'",
            because: "unrecognised single-dash flags should be reported");
        result.StdOut.Should().Contain("--folder",
            because: "scoped help should be shown so the user can see the correct flag");
    }

    [Fact]
    public async Task UnknownFlag_OnCreate_PrintsScopedHelp()
    {
        // Arrange — empty workspace

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, _ws.Root, "create", "Title", "-lane", "Todo");

        // Assert
        result.StdErr.Should().Contain("Unknown flag '-lane'");
        result.StdOut.Should().Contain("--lane",
            because: "scoped help for create should mention --lane");
    }

    public void Dispose() => _ws.Dispose();
}
