using System.Text;
using AwesomeAssertions;
using Xunit;

namespace Markban.UnitTests;

public class WipLimitTests
{
    private static string CreateBoard(IReadOnlyList<(string Name, bool Ordered, string? Type, int? Wip)> lanes)
    {
        var projectDir = Path.Combine(Path.GetTempPath(), "wip-" + Guid.NewGuid().ToString("N")[..8]);
        var boardDir = Path.Combine(projectDir, "board");

        var lanesJson = string.Join(",\n", lanes.Select(l =>
        {
            var parts = new List<string>
            {
                $"\"name\":\"{l.Name}\"",
                $"\"ordered\":{(l.Ordered ? "true" : "false")}"
            };
            if (l.Type != null)
            {
                parts.Add($"\"type\":\"{l.Type}\"");
            }

            if (l.Wip.HasValue)
            {
                parts.Add($"\"wip\":{l.Wip.Value}");
            }

            return "    {" + string.Join(",", parts) + "}";
        }));

        Directory.CreateDirectory(boardDir);
        File.WriteAllText(
            Path.Combine(projectDir, "markban.json"),
            $"{{\"rootPath\":\"./board\",\"lanes\":[{lanesJson}]}}");

        foreach (var lane in lanes)
        {
            Directory.CreateDirectory(Path.Combine(boardDir, lane.Name));
        }

        return boardDir;
    }

    private static void AddItem(string boardDir, string lane, string fileName, string content)
    {
        File.WriteAllText(Path.Combine(boardDir, lane, fileName), content, new UTF8Encoding(false));
    }

    // -------------------------------------------------------------------------
    // MoveCommand WIP enforcement
    // -------------------------------------------------------------------------

    [Fact]
    public void MoveCommand_WhenAtWipLimit_PrintsErrorAndDoesNotMove()
    {
        // Arrange
        var board = CreateBoard([
            ("Todo", true, "ready", null),
            ("Active", true, null, 1),
        ]);
        AddItem(board, "Todo", "1-task-a.md", "# 1 - Task A\n");
        AddItem(board, "Todo", "2-task-b.md", "# 2 - Task B\n");
        AddItem(board, "Active", "3-task-c.md", "# 3 - Task C\n");  // Active is full (1/1)

        var output = new System.IO.StringWriter();
        var originalOut = Console.Out;
        Console.SetOut(output);

        try
        {
            // Act
            MoveCommand.Execute(board, "1", "Active");

            // Assert
            var text = output.ToString();
            text.Should().Contain("WIP limit", because: "moving to a full lane should show the WIP error");
            text.Should().Contain("1/1", because: "error should show current/max counts");
            File.Exists(Path.Combine(board, "Active", "1-task-a.md"))
                .Should().BeFalse(because: "item should not have moved");
            File.Exists(Path.Combine(board, "Todo", "1-task-a.md"))
                .Should().BeTrue(because: "item should remain in Todo");
        }
        finally
        {
            Console.SetOut(originalOut);
            Directory.Delete(Path.GetDirectoryName(board)!, true);
        }
    }

    [Fact]
    public void MoveCommand_WithOverrideWip_BypassesLimitAndMoves()
    {
        // Arrange
        var board = CreateBoard([
            ("Todo", true, "ready", null),
            ("Active", true, null, 1),
        ]);
        AddItem(board, "Todo", "1-task-a.md", "# 1 - Task A\n");
        AddItem(board, "Active", "2-task-b.md", "# 2 - Task B\n");  // Active is full

        try
        {
            // Act
            MoveCommand.Execute(board, "1", "Active", overrideWip: true);

            // Assert
            File.Exists(Path.Combine(board, "Active", "1-task-a.md"))
                .Should().BeTrue(because: "--override-wip should allow the move");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(board)!, true);
        }
    }

    [Fact]
    public void MoveCommand_WhenBelowWipLimit_MovesNormally()
    {
        // Arrange — wip:2, currently 1 item
        var board = CreateBoard([
            ("Todo", true, "ready", null),
            ("Active", true, null, 2),
        ]);
        AddItem(board, "Todo", "1-task-a.md", "# 1 - Task A\n");
        AddItem(board, "Active", "2-task-b.md", "# 2 - Task B\n");

        try
        {
            // Act
            MoveCommand.Execute(board, "1", "Active");

            // Assert
            File.Exists(Path.Combine(board, "Active", "1-task-a.md"))
                .Should().BeTrue(because: "WIP limit is not reached so move should succeed");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(board)!, true);
        }
    }

    [Fact]
    public void MoveCommand_NoWipSet_NeverBlocks()
    {
        // Arrange — lane has no wip limit
        var board = CreateBoard([
            ("Todo", true, "ready", null),
            ("Active", true, null, null),
        ]);
        for (int i = 1; i <= 5; i++)
        {
            AddItem(board, "Active", $"{i}-item.md", $"# {i} - Item\n");
        }

        AddItem(board, "Todo", "6-new.md", "# 6 - New\n");

        try
        {
            // Act
            MoveCommand.Execute(board, "6", "Active");

            // Assert
            File.Exists(Path.Combine(board, "Active", "6-new.md"))
                .Should().BeTrue(because: "no WIP limit means no blocking");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(board)!, true);
        }
    }
}
