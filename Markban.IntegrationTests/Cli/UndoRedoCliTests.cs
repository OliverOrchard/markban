using System.Text;
using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class UndoRedoCliTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly List<string> _tempDirs = [];

    public UndoRedoCliTests(ToolBuildFixture build)
    {
        _build = build;
    }

    public void Dispose()
    {
        foreach (string dir in _tempDirs.Where(Directory.Exists))
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task Reorder_ThenUndo_ThenRedo_RoundTripsBoard()
    {
        // Arrange
        string board = ReorderBoardBuilder.Create(
            _tempDirs,
            "cli-undo-redo",
            subItemReorder: null,
            ("1-alpha.md", "alpha"),
            ("2-beta.md", "beta"),
            ("3-gamma.md", "gamma"));

        // Act
        var reorder = await CliRunner.RunAsync(_build.DllPath, board, "reorder", "Todo", "3,1,2");
        string[] afterReorder = ReorderBoardBuilder.TodoFiles(board);
        var undo = await CliRunner.RunAsync(_build.DllPath, board, "undo");
        string[] afterUndo = ReorderBoardBuilder.TodoFiles(board);
        string gammaAfterUndo = File.ReadAllText(Path.Combine(board, "Todo", "3-gamma.md"));
        var redo = await CliRunner.RunAsync(_build.DllPath, board, "redo");
        string[] afterRedo = ReorderBoardBuilder.TodoFiles(board);

        // Assert
        reorder.ExitCode.Should().Be(0, because: reorder.StdErr);
        afterReorder.Should().Equal("1-gamma.md", "2-alpha.md", "3-beta.md");
        undo.ExitCode.Should().Be(0, because: undo.StdErr);
        afterUndo.Should().Equal("1-alpha.md", "2-beta.md", "3-gamma.md");
        gammaAfterUndo.Should().Contain("# 3 - gamma");
        redo.ExitCode.Should().Be(0, because: redo.StdErr);
        afterRedo.Should().Equal("1-gamma.md", "2-alpha.md", "3-beta.md");
        File.ReadAllText(Path.Combine(board, "Todo", "1-gamma.md")).Should().Contain("# 1 - gamma");
    }

    [Fact]
    public async Task ForceSubItemReorder_ThenUndo_ReattachesSubItemToOriginalParent()
    {
        // Arrange
        string board = ReorderBoardBuilder.Create(
            _tempDirs,
            "cli-sub-undo",
            "force",
            ReorderBoardBuilder.ParentAndSubItem);

        // Act
        var reorder = await CliRunner.RunAsync(_build.DllPath, board, "reorder", "Todo", "2a,1,2,3");
        var undo = await CliRunner.RunAsync(_build.DllPath, board, "undo");

        // Assert
        reorder.ExitCode.Should().Be(0, because: reorder.StdErr);
        undo.ExitCode.Should().Be(0, because: undo.StdErr);
        ReorderBoardBuilder.TodoFiles(board).Should().Equal("1-alpha.md", "2-beta.md", "2a-beta-child.md", "3-gamma.md");
        File.ReadAllText(Path.Combine(board, "Todo", "2a-beta-child.md")).Should().Contain("# 2a - beta-child");
    }

    [Fact]
    public async Task Undo_WhenFileEditedAfterReorder_RefusesNamesFileExitsNonZeroAndChangesNothing()
    {
        // Arrange
        string board = ReorderBoardBuilder.Create(
            _tempDirs,
            "cli-undo-refuse",
            subItemReorder: null,
            ("1-alpha.md", "alpha"),
            ("2-beta.md", "beta"));
        await CliRunner.RunAsync(_build.DllPath, board, "reorder", "Todo", "2,1");
        File.AppendAllText(Path.Combine(board, "Todo", "1-beta.md"), "\nEdited after reorder\n");

        // Act
        var undo = await CliRunner.RunAsync(_build.DllPath, board, "undo");

        // Assert
        undo.ExitCode.Should().NotBe(0);
        undo.StdErr.Should().Contain("Todo/1-beta.md");
        ReorderBoardBuilder.TodoFiles(board).Should().Equal("1-beta.md", "2-alpha.md");
    }

    [Fact]
    public async Task Undo_AndRedo_WithEmptyStack_ExitNonZeroWithClearMessage()
    {
        // Arrange
        string board = ReorderBoardBuilder.Create(
            _tempDirs,
            "cli-undo-empty",
            subItemReorder: null,
            ("1-alpha.md", "alpha"));

        // Act
        var undo = await CliRunner.RunAsync(_build.DllPath, board, "undo");
        var redo = await CliRunner.RunAsync(_build.DllPath, board, "redo");

        // Assert
        undo.ExitCode.Should().NotBe(0);
        undo.StdErr.Should().Contain("Nothing to undo");
        redo.ExitCode.Should().NotBe(0);
        redo.StdErr.Should().Contain("Nothing to redo");
    }

    [Fact]
    public async Task UndoList_RedoList_AndDryRun_ShowStacksAndPreviewChanges()
    {
        // Arrange
        string board = ReorderBoardBuilder.Create(
            _tempDirs,
            "cli-undo-list",
            subItemReorder: null,
            ("1-alpha.md", "alpha"),
            ("2-beta.md", "beta"));
        await CliRunner.RunAsync(_build.DllPath, board, "reorder", "Todo", "2,1");

        // Act
        var undoList = await CliRunner.RunAsync(_build.DllPath, board, "undo", "--list");
        var redoList = await CliRunner.RunAsync(_build.DllPath, board, "redo", "--list");
        var dryUndo = await CliRunner.RunAsync(_build.DllPath, board, "undo", "--dry-run");

        // Assert
        undoList.ExitCode.Should().Be(0);
        undoList.StdOut.Should().Contain("Undo stack (1)").And.Contain("Redo stack (0)");
        redoList.ExitCode.Should().Be(0);
        redoList.StdOut.Should().Contain("Undo stack (1)").And.Contain("Redo stack (0)");
        dryUndo.ExitCode.Should().Be(0);
        dryUndo.StdOut.Should().Contain("Dry run").And.Contain("Todo/1-beta.md -> Todo/2-beta.md");
        ReorderBoardBuilder.TodoFiles(board).Should().Equal("1-beta.md", "2-alpha.md");
    }

    [Fact]
    public async Task HistoryDisabled_RecordsNothing_AndUndoExplainsWhy()
    {
        // Arrange
        string board = ReorderBoardBuilder.Create(
            _tempDirs,
            "cli-hist-off",
            subItemReorder: null,
            ("1-alpha.md", "alpha"),
            ("2-beta.md", "beta"));
        string parentDir = Path.GetDirectoryName(board)!;
        string rootForJson = board.Replace("\\", "/");
        File.WriteAllText(
            Path.Combine(parentDir, "markban.json"),
            $$"""{"rootPath":"{{rootForJson}}","history":{"enabled":false,"maxEntries":50} }""",
            new UTF8Encoding(false));

        // Act
        await CliRunner.RunAsync(_build.DllPath, board, "reorder", "Todo", "2,1");
        var undo = await CliRunner.RunAsync(_build.DllPath, board, "undo");

        // Assert
        undo.ExitCode.Should().NotBe(0);
        undo.StdErr.Should().Contain("disabled");
    }
}
