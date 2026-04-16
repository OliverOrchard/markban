using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class WebUiSmokeTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly List<string> _tempDirs = [];

    public WebUiSmokeTests(ToolBuildFixture build)
    {
        _build = build;
    }

    [Fact]
    public async Task SingleBoardMode_HidesBoardSwitcher()
    {
        // Arrange
        var projectDir = CreateTempDir("web-ui-single");
        var boardRoot = CreateBoardRoot(projectDir, "board");
        File.WriteAllText(Path.Combine(projectDir, "markban.json"), """{"rootPath":"./board"}""");
        File.WriteAllText(Path.Combine(boardRoot, "Todo", "1-single-board.md"), "# 1 - Single Board\n\n## Description\n\nOne item");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        // Act
        await page.GotoAsync(server.BaseUrl);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.card').length === 1");
        var isVisible = await page.Locator("#board-switcher-container").IsVisibleAsync();

        // Assert
        isVisible.Should().BeFalse(
            because: "single-board mode should not show the board switcher");
    }

    [Fact]
    public async Task BoardSelection_ReloadsItems_AndPersistsAcrossRefresh()
    {
        // Arrange
        var projectDir = CreateTempDir("web-ui-switch");
        var boardRoot = CreateBoardRoot(projectDir, "main-board");
        var backendRoot = CreateBoardRoot(projectDir, "backend-board");
        var frontendRoot = CreateBoardRoot(projectDir, "frontend-board");
        File.WriteAllText(Path.Combine(backendRoot, "Todo", "1-backend-item.md"), "# 1 - Backend Item\n\n## Description\n\nBackend");
        File.WriteAllText(Path.Combine(frontendRoot, "Todo", "1-frontend-item.md"), "# 1 - Frontend Item\n\n## Description\n\nFrontend");
        WriteMultiBoardConfig(projectDir, [("Backend", "backend-board"), ("Frontend", "frontend-board")]);
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        // Act
        await page.GotoAsync(server.BaseUrl);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('#board-select option').length === 2");
        await page.SelectOptionAsync("#board-select", "frontend");
        await page.Locator(".card[data-slug='frontend-item']").WaitForAsync();
        var selectedBoard = await page.Locator("#board-select").InputValueAsync();
        var backendCardCount = await page.Locator(".card[data-slug='backend-item']").CountAsync();
        await page.ReloadAsync();
        await page.WaitForFunctionAsync("() => document.querySelectorAll('#board-select option').length === 2");
        await page.Locator(".card[data-slug='frontend-item']").WaitForAsync();
        var persistedBoard = await page.Locator("#board-select").InputValueAsync();
        var frontendCardCount = await page.Locator(".card[data-slug='frontend-item']").CountAsync();

        // Assert
        selectedBoard.Should().Be("frontend");
        persistedBoard.Should().Be("frontend",
            because: "the active board should survive a manual refresh via localStorage");
        backendCardCount.Should().Be(0,
            because: "switching boards should replace the visible cards without a full page refresh");
        frontendCardCount.Should().Be(1);
    }

    [Fact]
    public async Task MoveAction_UsesTheActiveBoard()
    {
        // Arrange
        var projectDir = CreateTempDir("web-ui-move");
        var boardRoot = CreateBoardRoot(projectDir, "main-board");
        var frontendRoot = CreateBoardRoot(projectDir, "frontend-board");
        var backendRoot = CreateBoardRoot(projectDir, "backend-board");
        File.WriteAllText(Path.Combine(frontendRoot, "Todo", "1-frontend-task.md"), "# 1 - Frontend Task\n\n## Description\n\nFrontend");
        File.WriteAllText(Path.Combine(backendRoot, "Todo", "1-backend-task.md"), "# 1 - Backend Task\n\n## Description\n\nBackend");
        WriteMultiBoardConfig(projectDir, [("Frontend", "frontend-board"), ("Backend", "backend-board")]);
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        // Act
        await page.GotoAsync(server.BaseUrl);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('#board-select option').length === 2");
        await page.SelectOptionAsync("#board-select", "backend");
        await page.Locator(".card[data-slug='backend-task']").WaitForAsync();
        await page.ClickAsync(".card[data-slug='backend-task']");
        await page.Locator("#detail-pane.open").WaitForAsync();
        await page.SelectOptionAsync("#move-target", "In Progress");
        await page.ClickAsync("#move-btn");
        await page.Locator(".column[data-lane='In Progress'] .card[data-slug='backend-task']").WaitForAsync();

        // Assert
        File.Exists(Path.Combine(backendRoot, "In Progress", "1-backend-task.md")).Should().BeTrue(
            because: "the active board selection should drive which board receives the move");
        File.Exists(Path.Combine(frontendRoot, "Todo", "1-frontend-task.md")).Should().BeTrue(
            because: "moving in one board from the UI must not change the other board");
    }

    public void Dispose()
    {
        foreach (var tempDir in _tempDirs)
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    private string CreateTempDir(string prefix)
    {
        var path = Path.Combine(Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(path);
        _tempDirs.Add(path);
        return path;
    }

    private static string CreateBoardRoot(string projectDir, string folderName)
    {
        var root = Path.Combine(projectDir, folderName);
        foreach (var lane in new[] { "Todo", "In Progress", "Testing", "Done", "Ideas", "Rejected" })
        {
            Directory.CreateDirectory(Path.Combine(root, lane));
        }

        return root;
    }

    private static void WriteMultiBoardConfig(string projectDir, IReadOnlyList<(string Name, string Path)> boards)
    {
        var boardsJson = string.Join(",", boards.Select(board => $"{{\"name\":\"{board.Name}\",\"path\":\"./{board.Path}\"}}"));
        File.WriteAllText(
            Path.Combine(projectDir, "markban.json"),
            $"{{\"rootPath\":\"./main-board\",\"boards\":[{boardsJson}]}}");
    }
}
