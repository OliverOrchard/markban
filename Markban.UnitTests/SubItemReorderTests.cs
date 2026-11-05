using System.Text;
using AwesomeAssertions;
using Xunit;

namespace Markban.UnitTests;

[Collection("ConsoleOutput")]
public class SubItemReorderTests : IDisposable
{
    private readonly List<string> _tempDirs = [];

    [Fact]
    public void LoadSettings_WithoutKey_DefaultsToWarn()
    {
        // Arrange
        var board = CreateBoard(null);

        // Act
        var settings = WorkItemStore.LoadSettings(board);

        // Assert
        settings.SubItemReorder.Should().Be("warn");
    }

    [Theory]
    [InlineData("warn")]
    [InlineData("block")]
    [InlineData("force")]
    public void LoadSettings_WithValidMode_ReadsIt(string mode)
    {
        // Arrange
        var board = CreateBoard(mode);

        // Act
        var settings = WorkItemStore.LoadSettings(board);

        // Assert
        settings.SubItemReorder.Should().Be(mode);
        SubItemReorderMode.IsValid(settings.SubItemReorder).Should().BeTrue();
    }

    [Fact]
    public void Handler_WithInvalidMode_ReturnsClearError()
    {
        // Arrange
        var board = CreateBoard("sometimes");
        AddPrimaries(board);

        // Act
        var result = WebReorderHandler.Execute(board, new WebReorderRequest("Todo", ["2", "1"]));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Invalid subItemReorder 'sometimes'").And.Contain("warn, block, force");
    }

    [Fact]
    public void Handler_BlockMode_RejectsSubItemReorder_AndLeavesFilesUntouched()
    {
        // Arrange
        var board = CreateBoard("block");
        AddWithSubItems(board);

        // Act
        var result = WebReorderHandler.Execute(board, new WebReorderRequest("Todo", ["2a", "1", "2", "3"], "2a", true));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Sub-items stay with their parent");
        TodoFiles(board).Should().Equal(["1-alpha.md", "2-beta.md", "2a-beta-child.md", "3-gamma.md"]);
    }

    [Fact]
    public void Handler_WarnMode_WithoutConfirmation_Rejects()
    {
        // Arrange
        var board = CreateBoard("warn");
        AddWithSubItems(board);

        // Act
        var result = WebReorderHandler.Execute(board, new WebReorderRequest("Todo", ["2a", "1", "2", "3"], "2a"));

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("confirmation");
        TodoFiles(board).Should().Contain("2a-beta-child.md");
    }

    [Theory]
    [InlineData("warn", true)]
    [InlineData("force", false)]
    public void Handler_DetachesAndRenumbers_WhenConfirmedOrForced(string mode, bool confirm)
    {
        // Arrange
        var board = CreateBoard(mode);
        AddWithSubItems(board);
        File.WriteAllText(Path.Combine(board, "Todo", "2b-beta-other.md"), "# 2b - beta-other\n", new UTF8Encoding(false));

        // Act
        var result = WebReorderHandler.Execute(board, new WebReorderRequest("Todo", ["2a", "1", "2", "2b", "3"], "2a", confirm));

        // Assert
        result.IsSuccess.Should().BeTrue(because: result.Message);
        TodoFiles(board).Should().Equal(
            ["1-beta-child.md", "2-alpha.md", "3-beta.md", "3b-beta-other.md", "4-gamma.md"],
            because: "the detached sub-item becomes primary 1, while 2b stays attached to its parent beta");
        File.ReadAllText(Path.Combine(board, "Todo", "1-beta-child.md")).Should().StartWith("# 1 - beta-child");
        File.ReadAllText(Path.Combine(board, "Todo", "3b-beta-other.md")).Should().StartWith("# 3b - beta-other");
    }

    [Fact]
    public void DetachSubItem_PromotesToNextFreeNumber_AndRewritesHeading()
    {
        // Arrange
        var board = CreateBoard("force");
        AddWithSubItems(board);

        // Act
        var result = DetachSubItemCommand.Execute(board, "Todo", "2a");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.NewId.Should().Be("4");
        TodoFiles(board).Should().Equal(["1-alpha.md", "2-beta.md", "3-gamma.md", "4-beta-child.md"]);
        File.ReadAllText(Path.Combine(board, "Todo", "4-beta-child.md")).Should().StartWith("# 4 - beta-child");
    }

    [Fact]
    public void DetachSubItem_WithUnknownId_Fails()
    {
        // Arrange
        var board = CreateBoard("force");
        AddPrimaries(board);

        // Act
        var result = DetachSubItemCommand.Execute(board, "Todo", "9z");

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void Init_WritesSubItemReorderWarnExplicitly()
    {
        // Arrange
        var dir = Path.Combine(Path.GetTempPath(), "wb-init-sub-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);

        // Act
        InitCommand.Execute(dir, boardPath: null, name: null, dryRun: false);

        // Assert
        File.ReadAllText(Path.Combine(dir, "markban.json")).Should().Contain("\"subItemReorder\": \"warn\"");
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs.Where(Directory.Exists))
        {
            Directory.Delete(dir, true);
        }
    }

    private string CreateBoard(string? mode)
    {
        var parent = Path.Combine(Path.GetTempPath(), "wb-sub-" + Guid.NewGuid().ToString("N")[..8]);
        var board = Path.Combine(parent, "board");
        _tempDirs.Add(parent);
        foreach (var lane in new[] { "Todo", "In Progress", "Testing", "Done", "Ideas", "Rejected" })
        {
            Directory.CreateDirectory(Path.Combine(board, lane));
        }

        var modeJson = mode is null ? string.Empty : $",\"subItemReorder\":\"{mode}\"";
        File.WriteAllText(Path.Combine(parent, "markban.json"), $$"""{"rootPath":"./board"{{modeJson}}}""", new UTF8Encoding(false));
        return board;
    }

    private static void AddPrimaries(string board)
    {
        File.WriteAllText(Path.Combine(board, "Todo", "1-alpha.md"), "# 1 - alpha\n", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(board, "Todo", "2-beta.md"), "# 2 - beta\n", new UTF8Encoding(false));
    }

    private static void AddWithSubItems(string board)
    {
        AddPrimaries(board);
        File.WriteAllText(Path.Combine(board, "Todo", "2a-beta-child.md"), "# 2a - beta-child\n", new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(board, "Todo", "3-gamma.md"), "# 3 - gamma\n", new UTF8Encoding(false));
    }

    private static string[] TodoFiles(string board)
    {
        return Directory.GetFiles(Path.Combine(board, "Todo"), "*.md")
            .Select(Path.GetFileName).OfType<string>().Order().ToArray();
    }
}
