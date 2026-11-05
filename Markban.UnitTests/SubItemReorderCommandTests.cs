using System.Text;
using AwesomeAssertions;
using Xunit;

namespace Markban.UnitTests;

[Collection("ConsoleOutput")]
public class SubItemReorderCommandTests : IDisposable
{
    private const string Order = "2a,1,2,3";
    private readonly List<string> _tempDirs = [];

    [Fact]
    public void BlockMode_FailsWithClearError_AndChangesNothing()
    {
        // Arrange
        var board = CreateBoard("block");

        // Act
        var (outcome, _, error) = Run(board, new SubItemReorderOptions(false, true, new StringReader("y"), true));

        // Assert
        outcome.Should().Be(SubItemReorderOutcome.Failed);
        error.Should().Contain("Sub-items stay with their parent").And.Contain("warn");
        TodoFiles(board).Should().Equal(OriginalFiles);
    }

    [Theory]
    [InlineData("y")]
    [InlineData("YES")]
    public void WarnMode_Confirmed_DetachesAndReorders(string answer)
    {
        // Arrange
        var board = CreateBoard("warn");

        // Act
        var (outcome, output, _) = Run(board, new SubItemReorderOptions(false, false, new StringReader(answer), true));

        // Assert
        outcome.Should().Be(SubItemReorderOutcome.Done);
        output.Should().Contain("Continue? [y/N]").And.Contain("2a -> 4");
        TodoFiles(board).Should().Equal(["1-beta-child.md", "2-alpha.md", "3-beta.md", "4-gamma.md"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("n")]
    public void WarnMode_Declined_CancelsWithoutChanges(string answer)
    {
        // Arrange
        var board = CreateBoard("warn");

        // Act
        var (outcome, output, _) = Run(board, new SubItemReorderOptions(false, false, new StringReader(answer), true));

        // Assert
        outcome.Should().Be(SubItemReorderOutcome.Cancelled);
        output.Should().Contain("Cancelled");
        TodoFiles(board).Should().Equal(OriginalFiles);
    }

    [Fact]
    public void WarnMode_NonInteractive_TreatsAsNo_AndMentionsYesFlag()
    {
        // Arrange
        var board = CreateBoard("warn");

        // Act
        var (outcome, output, _) = Run(board, new SubItemReorderOptions(false, false, new StringReader("y"), false));

        // Assert
        outcome.Should().Be(SubItemReorderOutcome.Failed);
        output.Should().Contain("--yes");
        TodoFiles(board).Should().Equal(OriginalFiles);
    }

    [Fact]
    public void WarnMode_WithYes_SkipsPrompt()
    {
        // Arrange
        var board = CreateBoard("warn");

        // Act
        var (outcome, output, _) = Run(board, new SubItemReorderOptions(false, true, new ThrowingReader(), false));

        // Assert
        outcome.Should().Be(SubItemReorderOutcome.Done);
        TodoFiles(board).Should().Contain("1-beta-child.md");
    }

    [Fact]
    public void ForceMode_DetachesWithoutPrompt()
    {
        // Arrange
        var board = CreateBoard("force");

        // Act
        var (outcome, output, _) = Run(board, new SubItemReorderOptions(false, false, new ThrowingReader(), false));

        // Assert
        outcome.Should().Be(SubItemReorderOutcome.Done);
        TodoFiles(board).Should().Contain("1-beta-child.md");
    }

    [Theory]
    [InlineData("warn")]
    [InlineData("force")]
    public void DryRun_ShowsPlan_NeverPrompts_AndChangesNothing(string mode)
    {
        // Arrange
        var board = CreateBoard(mode);

        // Act
        var (outcome, output, _) = Run(board, new SubItemReorderOptions(true, false, new ThrowingReader(), true));

        // Assert
        outcome.Should().Be(SubItemReorderOutcome.Done);
        output.Should().Contain("2a -> 4").And.Contain("Dry run");
        TodoFiles(board).Should().Equal(OriginalFiles);
    }

    [Fact]
    public void UnknownSubItem_Fails()
    {
        // Arrange
        var board = CreateBoard("force");

        // Act
        var outcome = SubItemReorderCommand.Execute(board, "Todo", "9z,1", new SubItemReorderOptions(false, true, TextReader.Null, false));

        // Assert
        outcome.Should().Be(SubItemReorderOutcome.Failed);
        TodoFiles(board).Should().Equal(OriginalFiles);
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs.Where(Directory.Exists))
        {
            Directory.Delete(dir, true);
        }
    }

    private sealed class ThrowingReader : TextReader
    {
        public override string ReadLine() => throw new InvalidOperationException("The prompt must not be read.");
    }

    private static readonly string[] OriginalFiles = ["1-alpha.md", "2-beta.md", "2a-beta-child.md", "3-gamma.md"];

    private static (SubItemReorderOutcome Outcome, string Output, string Error) Run(string board, SubItemReorderOptions options)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        var output = new StringWriter();
        var error = new StringWriter();
        Console.SetOut(output);
        Console.SetError(error);
        try
        {
            var outcome = SubItemReorderCommand.Execute(board, "Todo", Order, options);
            return (outcome, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    private string CreateBoard(string mode)
    {
        var parent = Path.Combine(Path.GetTempPath(), "wb-subcli-" + Guid.NewGuid().ToString("N")[..8]);
        var board = Path.Combine(parent, "board");
        _tempDirs.Add(parent);
        foreach (var lane in new[] { "Todo", "In Progress", "Testing", "Done", "Ideas", "Rejected" })
        {
            Directory.CreateDirectory(Path.Combine(board, lane));
        }

        File.WriteAllText(Path.Combine(parent, "markban.json"), $$"""{"rootPath":"./board","subItemReorder":"{{mode}}"}""", new UTF8Encoding(false));
        foreach (var file in OriginalFiles)
        {
            var id = file[..file.IndexOf('-')];
            File.WriteAllText(Path.Combine(board, "Todo", file), $"# {id} - {file[(id.Length + 1)..^3]}\n", new UTF8Encoding(false));
        }

        return board;
    }

    private static string[] TodoFiles(string board)
    {
        return Directory.GetFiles(Path.Combine(board, "Todo"), "*.md")
            .Select(Path.GetFileName).OfType<string>().Order().ToArray();
    }
}
