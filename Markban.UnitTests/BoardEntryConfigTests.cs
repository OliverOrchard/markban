using AwesomeAssertions;
using Xunit;

public class BoardEntryConfigTests
{
    [Fact]
    public void LoadBoards_WithRelativePaths_ResolvesAgainstConfigDirectory()
    {
        // Arrange
        var projectDir = CreateTempDir("boards-config");
        var backendDir = Path.Combine(projectDir, "services", "api");
        var frontendDir = Path.Combine(projectDir, "services", "web");
        Directory.CreateDirectory(backendDir);
        Directory.CreateDirectory(frontendDir);
        File.WriteAllText(Path.Combine(projectDir, "markban.json"), """
            {
              "boards": [
                { "name": "Backend", "path": "services/api" },
                { "name": "Frontend", "path": "services/web" }
              ]
            }
            """);

        try
        {
            // Act
            var boards = WorkItemStore.LoadBoards(projectDir);

            // Assert
            boards.Should().HaveCount(2);
            boards[0].Name.Should().Be("Backend");
            boards[0].Key.Should().Be("backend");
            boards[0].ResolvedPath.Should().Be(Path.GetFullPath(backendDir));
            boards[1].Name.Should().Be("Frontend");
            boards[1].Key.Should().Be("frontend");
            boards[1].ResolvedPath.Should().Be(Path.GetFullPath(frontendDir));
        }
        finally
        {
            Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public void LoadBoards_WhenNoBoardsKey_ReturnsEmptyList()
    {
        // Arrange
        var projectDir = CreateTempDir("boards-empty");
        File.WriteAllText(Path.Combine(projectDir, "markban.json"), """{"rootPath":"./board"}""");

        try
        {
            // Act
            var boards = WorkItemStore.LoadBoards(projectDir);

            // Assert
            boards.Should().BeEmpty(because: "single-board mode should not synthesize configured boards");
        }
        finally
        {
            Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public void LoadBoards_WithMissingPath_ThrowsClearError()
    {
        // Arrange
        var projectDir = CreateTempDir("boards-invalid");
        File.WriteAllText(Path.Combine(projectDir, "markban.json"), """
            {
              "boards": [
                { "name": "Backend" }
              ]
            }
            """);

        try
        {
            // Act
            var act = () => WorkItemStore.LoadBoards(projectDir);

            // Assert
            act.Should().Throw<InvalidDataException>()
                .WithMessage("*boards[0]*path*");
        }
        finally
        {
            Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public void ResolveConfiguredBoardRoot_WithNestedConfig_UsesThatRootPath()
    {
        // Arrange
        var projectDir = CreateTempDir("boards-nested");
        var boardRoot = Path.Combine(projectDir, "board-root");
        Directory.CreateDirectory(boardRoot);
        File.WriteAllText(Path.Combine(projectDir, "markban.json"), """{"rootPath":"./board-root"}""");

        try
        {
            // Act
            var resolvedRoot = WorkItemStore.ResolveConfiguredBoardRoot(projectDir);

            // Assert
            resolvedRoot.Should().Be(Path.GetFullPath(boardRoot));
        }
        finally
        {
            Directory.Delete(projectDir, true);
        }
    }

    [Fact]
    public void ResolveConfiguredBoardRoot_WithoutNestedConfig_UsesDirectBoardRoot()
    {
        // Arrange
        var boardRoot = CreateTempDir("boards-direct");
        Directory.CreateDirectory(Path.Combine(boardRoot, "Todo"));

        try
        {
            // Act
            var resolvedRoot = WorkItemStore.ResolveConfiguredBoardRoot(boardRoot);

            // Assert
            resolvedRoot.Should().Be(Path.GetFullPath(boardRoot),
                because: "boards without their own config should still be addressable directly");
        }
        finally
        {
            Directory.Delete(boardRoot, true);
        }
    }

    [Fact]
    public void ResolveConfiguredBoardRoot_WhenPathIsMissing_ThrowsClearError()
    {
        // Arrange
        var boardPath = Path.Combine(Path.GetTempPath(), "boards-missing-" + Guid.NewGuid().ToString("N")[..8]);

        // Act
        var act = () => WorkItemStore.ResolveConfiguredBoardRoot(boardPath);

        // Assert
        act.Should().Throw<DirectoryNotFoundException>()
            .WithMessage("*Board path not found*");
    }

    private static string CreateTempDir(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        return path;
    }
}
