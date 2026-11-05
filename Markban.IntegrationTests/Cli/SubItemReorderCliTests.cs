using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class SubItemReorderCliTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly List<string> _tempDirs = [];

    public SubItemReorderCliTests(ToolBuildFixture build)
    {
        _build = build;
    }

    [Fact]
    public async Task Reorder_BlockMode_WithSubItem_FailsNonZeroAndChangesNothing()
    {
        // Arrange
        var board = ReorderBoardBuilder.Create(_tempDirs, "cli-sub-block", "block", ReorderBoardBuilder.ParentAndSubItem);

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, board, "reorder", "Todo", "2a,1,2,3");

        // Assert
        result.ExitCode.Should().NotBe(0);
        result.StdErr.Should().Contain("Sub-items stay with their parent");
        ReorderBoardBuilder.TodoFiles(board).Should().Equal("1-alpha.md", "2-beta.md", "2a-beta-child.md", "3-gamma.md");
    }

    [Fact]
    public async Task Reorder_WarnMode_WithYes_DetachesSubItem()
    {
        // Arrange
        var board = ReorderBoardBuilder.Create(_tempDirs, "cli-sub-yes", "warn", ReorderBoardBuilder.ParentAndSubItem);

        // Act
        var result = await CliRunner.RunAsync(_build.DllPath, board, "reorder", "Todo", "2a,1,2,3", "--yes");

        // Assert
        result.ExitCode.Should().Be(0, because: result.StdErr);
        ReorderBoardBuilder.TodoFiles(board).Should().Equal("1-beta-child.md", "2-alpha.md", "3-beta.md", "4-gamma.md");
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs.Where(Directory.Exists))
        {
            Directory.Delete(dir, true);
        }
    }
}
