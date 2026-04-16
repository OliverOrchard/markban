using AwesomeAssertions;
using Xunit;

public class LaneConfigTests
{
    // -------------------------------------------------------------------------
    // BoardConfig.DefaultLanes — static contract tests
    // -------------------------------------------------------------------------

    [Fact]
    public void DefaultLanes_ContainsExactlyOneReadyLane()
    {
        // Act
        var readyLanes = BoardConfig.DefaultLanes.Where(l => l.Type == "ready").ToList();

        // Assert
        readyLanes.Should().ContainSingle(because: "exactly one lane should have type 'ready'");
        readyLanes[0].Name.Should().Be("Todo");
    }

    [Fact]
    public void DefaultLanes_ContainsExactlyOneDoneLane()
    {
        // Act
        var doneLanes = BoardConfig.DefaultLanes.Where(l => l.Type == "done").ToList();

        // Assert
        doneLanes.Should().ContainSingle(because: "exactly one lane should have type 'done'");
        doneLanes[0].Name.Should().Be("Done");
    }

    [Fact]
    public void DefaultLanes_IdeasAndRejected_AreUnorderedAndNotPickable()
    {
        // Arrange
        var unorderedLanes = BoardConfig.DefaultLanes.Where(l => !l.Ordered).ToList();

        // Assert
        unorderedLanes.Should().HaveCount(2);
        unorderedLanes.Should().AllSatisfy(l => l.Pickable.Should().BeFalse(
            because: "unordered holding lanes should not be pickable"));
    }

    [Fact]
    public void DefaultLanes_AllPickableLanes_AreOrdered()
    {
        // Assert
        BoardConfig.DefaultLanes
            .Where(l => l.Pickable)
            .Should().AllSatisfy(l => l.Ordered.Should().BeTrue(
                because: "all pickable lanes should use ordered (numbered) filenames"));
    }

    // -------------------------------------------------------------------------
    // WorkItemStore.LoadConfig — reads custom lane types from markban.json
    // -------------------------------------------------------------------------

    [Fact]
    public void LoadConfig_WithCustomReadyAndDoneLanes_ParsesTypesCorrectly()
    {
        // Arrange
        var projectDir = Path.Combine(Path.GetTempPath(), "lct-" + Guid.NewGuid().ToString("N")[..8]);
        var boardDir = Path.Combine(projectDir, "board");
        Directory.CreateDirectory(boardDir);
        File.WriteAllText(Path.Combine(projectDir, "markban.json"), """
            {
                "rootPath": "./board",
                "lanes": [
                    {"name":"Backlog","ordered":true,"type":"ready"},
                    {"name":"Doing","ordered":true},
                    {"name":"Shipped","ordered":true,"type":"done"},
                    {"name":"Shelf","ordered":false,"pickable":false}
                ]
            }
            """);
        try
        {
            // Act
            var lanes = WorkItemStore.LoadConfig(boardDir);

            // Assert
            lanes.Should().HaveCount(4);
            lanes.Single(l => l.Name == "Backlog").Type.Should().Be("ready");
            lanes.Single(l => l.Name == "Doing").Type.Should().BeNull();
            lanes.Single(l => l.Name == "Shipped").Type.Should().Be("done");
            lanes.Single(l => l.Name == "Shelf").Pickable.Should().BeFalse();
            lanes.Single(l => l.Name == "Shelf").Ordered.Should().BeFalse();
        }
        finally
        {
            Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public void LoadConfig_WhenNoLanesKey_ReturnsDefaultLanes()
    {
        // Arrange
        var projectDir = Path.Combine(Path.GetTempPath(), "lct-" + Guid.NewGuid().ToString("N")[..8]);
        var boardDir = Path.Combine(projectDir, "board");
        Directory.CreateDirectory(boardDir);
        File.WriteAllText(Path.Combine(projectDir, "markban.json"), """{"rootPath": "./board"}""");
        try
        {
            // Act
            var lanes = WorkItemStore.LoadConfig(boardDir);

            // Assert
            lanes.Should().BeEquivalentTo(BoardConfig.DefaultLanes,
                because: "missing 'lanes' key should fall back to defaults");
        }
        finally
        {
            Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public void LoadConfig_WhenNoConfigFile_ReturnsDefaultLanes()
    {
        // Arrange
        var boardDir = Path.Combine(Path.GetTempPath(), "lct-noconfig-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(boardDir);
        try
        {
            // Act
            var lanes = WorkItemStore.LoadConfig(boardDir);

            // Assert
            lanes.Should().BeEquivalentTo(BoardConfig.DefaultLanes,
                because: "absent config file should fall back to defaults");
        }
        finally
        {
            Directory.Delete(boardDir, true);
        }
    }

    [Fact]
    public void EnsureLaneDirectories_CreatesMissingLaneDirs_SilentlyAndIdempotently()
    {
        // Arrange
        var projectDir = Path.Combine(Path.GetTempPath(), "lct-ensure-" + Guid.NewGuid().ToString("N")[..8]);
        var boardDir = Path.Combine(projectDir, "board");
        Directory.CreateDirectory(boardDir);
        File.WriteAllText(Path.Combine(projectDir, "markban.json"), """
            {
                "rootPath": "./board",
                "lanes": [
                    {"name":"Todo","ordered":true,"type":"ready"},
                    {"name":"Done","ordered":true,"type":"done"}
                ]
            }
            """);

        try
        {
            // Act — first call creates dirs; second call is idempotent
            WorkItemStore.EnsureLaneDirectories(boardDir);
            WorkItemStore.EnsureLaneDirectories(boardDir);

            // Assert
            Directory.Exists(Path.Combine(boardDir, "Todo")).Should().BeTrue();
            Directory.Exists(Path.Combine(boardDir, "Done")).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(projectDir, true);
        }
    }
}
