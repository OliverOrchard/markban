using System.Text;
using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class WebUiTagsAndBlockedTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly List<string> _tempDirs = [];

    public WebUiTagsAndBlockedTests(ToolBuildFixture build)
    {
        _build = build;
    }

    [Fact]
    public async Task LaneHeader_ShowsBlockedSubCount_WhenLaneHasBlockedItems()
    {
        // Arrange
        var boardRoot = CreateBoard("blocked-count");
        AddItem(boardRoot, "Todo", "1-blocked-item.md",
            "---\nblocked: waiting on API keys\n---\n\n# 1 - Blocked Item\n\n## Description\n\nBlocked");
        AddItem(boardRoot, "Todo", "2-normal-item.md",
            "# 2 - Normal Item\n\n## Description\n\nNot blocked");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        // Act
        await page.GotoAsync(server.BaseUrl);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.card').length === 2");
        var todoHeader = await page.Locator(".column[data-lane='Todo'] h2").InnerTextAsync();

        // Assert
        todoHeader.ToLower().Should().Contain("1 blocked",
            because: "the lane header should show a blocked sub-count when the lane contains blocked items");
    }

    [Fact]
    public async Task TagBadge_Click_FiltersBoard_ToOnlyShowItemsWithThatTag()
    {
        // Arrange
        var boardRoot = CreateBoard("tag-filter");
        AddItem(boardRoot, "Todo", "1-tagged-item.md",
            "---\ntags: [bug, backend]\n---\n\n# 1 - Tagged Item\n\n## Description\n\nHas bug tag");
        AddItem(boardRoot, "Todo", "2-other-item.md",
            "---\ntags: [feature]\n---\n\n# 2 - Other Item\n\n## Description\n\nHas feature tag");
        AddItem(boardRoot, "Todo", "3-untagged-item.md",
            "# 3 - Untagged Item\n\n## Description\n\nNo tags");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        // Act
        await page.GotoAsync(server.BaseUrl);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.card').length === 3");
        await page.Locator(".badge-tag").First.ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.card:not(.hidden)').length < 3");
        var visibleCount = await page.Locator(".card:not(.hidden)").CountAsync();
        var hiddenCount = await page.Locator(".card.hidden").CountAsync();

        // Assert
        visibleCount.Should().Be(1,
            because: "clicking a tag badge should filter the board to show only items with that tag");
        hiddenCount.Should().Be(2,
            because: "items without the clicked tag should be hidden");
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs)
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, true);
            }
        }
    }

    private string CreateBoard(string prefix)
    {
        var parentDir = Path.Combine(Path.GetTempPath(), $"mb-ui-{prefix}-" + Guid.NewGuid().ToString("N")[..8]);
        var boardRoot = Path.Combine(parentDir, "board");
        _tempDirs.Add(parentDir);

        foreach (var lane in new[] { "Todo", "In Progress", "Testing", "Done", "Ideas", "Rejected" })
        {
            Directory.CreateDirectory(Path.Combine(boardRoot, lane));
        }

        var rootForJson = boardRoot.Replace("\\", "/");
        var config = $$"""{"rootPath":"{{rootForJson}}"}""";
        File.WriteAllText(Path.Combine(parentDir, "markban.json"), config, new UTF8Encoding(false));

        return boardRoot;
    }

    private static void AddItem(string boardRoot, string lane, string fileName, string content)
    {
        File.WriteAllText(Path.Combine(boardRoot, lane, fileName), content, new UTF8Encoding(false));
    }
}
