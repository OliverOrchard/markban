using AwesomeAssertions;
using Xunit;

namespace Markban.UnitTests;

public class GitHistoryCommandTests
{
    [Theory]
    [InlineData("work-items/Todo/1-my-task.md", true)]
    [InlineData("work-items/Done/42-feature.md", true)]
    [InlineData("work-items/ideas/cool-idea.md", true)]
    [InlineData("src/SomeProject/Program.cs", false)]
    [InlineData("work-items/Todo/readme.txt", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsWorkItemPath_ClassifiesCorrectly(string? path, bool expected)
    {
        // Act
        var result = GitHistoryCommand.IsWorkItemPath(path);

        // Assert
        result.Should().Be(expected,
            because: $"'{path}' {(expected ? "is" : "is not")} a work-items markdown file");
    }

    [Theory]
    [InlineData("work-items/Todo/1-task.md", "Todo")]
    [InlineData("work-items/Done/42-feature.md", "Done")]
    [InlineData("work-items/In Progress/5-wip.md", "In Progress")]
    [InlineData("work-items/ideas/cool.md", "ideas")]
    [InlineData("src/Program.cs", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ExtractLane_ReturnsCorrectLane(string? path, string? expectedLane)
    {
        // Act
        var lane = GitHistoryCommand.ExtractLane(path);

        // Assert
        lane.Should().Be(expectedLane,
            because: $"the lane for '{path}' should be '{expectedLane}'");
    }

    [Fact]
    public void ClassifyChange_Added_ReturnsCreated()
    {
        // Arrange
        var change = new GitHistoryCommand.FileChange("A", null, "work-items/Todo/1-task.md");

        // Act
        var (action, fromLane, toLane) = GitHistoryCommand.ClassifyChange(change);

        // Assert
        action.Should().Be("Created", because: "status A means a new file was added");
        fromLane.Should().BeNull();
        toLane.Should().Be("Todo");
    }

    [Fact]
    public void ClassifyChange_Deleted_ReturnsDeleted()
    {
        // Arrange
        var change = new GitHistoryCommand.FileChange("D", null, "work-items/Done/5-old.md");

        // Act
        var (action, fromLane, toLane) = GitHistoryCommand.ClassifyChange(change);

        // Assert
        action.Should().Be("Deleted", because: "status D means the file was deleted");
        fromLane.Should().Be("Done");
        toLane.Should().BeNull();
    }

    [Fact]
    public void ClassifyChange_Modified_ReturnsModified()
    {
        // Arrange
        var change = new GitHistoryCommand.FileChange("M", null, "work-items/Testing/3-wip.md");

        // Act
        var (action, fromLane, toLane) = GitHistoryCommand.ClassifyChange(change);

        // Assert
        action.Should().Be("Modified", because: "status M means the file contents changed");
        toLane.Should().Be("Testing");
    }

    [Fact]
    public void ClassifyChange_RenameAcrossLanes_ReturnsMoved()
    {
        // Arrange
        var change = new GitHistoryCommand.FileChange(
            "R100", "work-items/Todo/1-task.md", "work-items/Done/1-task.md");

        // Act
        var (action, fromLane, toLane) = GitHistoryCommand.ClassifyChange(change);

        // Assert
        action.Should().Be("Moved", because: "a rename across lanes is a work item move");
        fromLane.Should().Be("Todo");
        toLane.Should().Be("Done");
    }

    [Fact]
    public void ClassifyChange_RenameWithinLane_ReturnsRenamed()
    {
        // Arrange
        var change = new GitHistoryCommand.FileChange(
            "R095", "work-items/Todo/1-old-name.md", "work-items/Todo/1-new-name.md");

        // Act
        var (action, fromLane, toLane) = GitHistoryCommand.ClassifyChange(change);

        // Assert
        action.Should().Be("Renamed", because: "a rename within the same lane is just a rename");
        fromLane.Should().Be("Todo");
        toLane.Should().Be("Todo");
    }

    [Fact]
    public void IsWorkItemPath_HandlesBackslashes()
    {
        // Arrange
        var path = "work-items\\Todo\\1-task.md";

        // Act
        var result = GitHistoryCommand.IsWorkItemPath(path);

        // Assert
        result.Should().BeTrue(because: "backslashes should be normalized to forward slashes");
    }
}
