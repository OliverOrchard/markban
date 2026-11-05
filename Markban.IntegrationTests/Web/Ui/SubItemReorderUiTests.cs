using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class SubItemReorderUiTests : IDisposable
{
    private const string SubItemCard = ".card[data-slug='beta-child']";
    private const string FirstCard = ".card[data-slug='alpha']";
    private static readonly string[] OriginalFiles = ["1-alpha.md", "2-beta.md", "2a-beta-child.md", "3-gamma.md"];
    private static readonly string[] DetachedFiles = ["1-beta-child.md", "2-alpha.md", "3-beta.md", "4-gamma.md"];

    private readonly ToolBuildFixture _build;
    private readonly List<string> _tempDirs = [];

    public SubItemReorderUiTests(ToolBuildFixture build)
    {
        _build = build;
    }

    [Fact]
    public async Task BlockMode_SubItemCard_IsNotDraggable_WithTooltipAndNotAllowedCursor()
    {
        // Arrange
        await using var session = await StartAsync("block");

        // Act
        var card = session.Page.Locator(SubItemCard);
        var draggable = await card.GetAttributeAsync("draggable");
        var title = await card.GetAttributeAsync("title");
        var cursor = await card.EvaluateAsync<string>("el => getComputedStyle(el).cursor");
        var parentDraggable = await session.Page.Locator(FirstCard).GetAttributeAsync("draggable");

        // Assert
        draggable.Should().Be("false");
        title.Should().Be("Sub-items stay with their parent. Change subItemReorder to reorder.");
        cursor.Should().Be("not-allowed");
        parentDraggable.Should().Be("true", because: "only sub-items are locked");
    }

    [Fact]
    public async Task WarnMode_Cancel_RestoresOrder_AndLeavesFilesUntouched()
    {
        // Arrange
        await using var session = await StartAsync(null);

        // Act
        await DragSubItemAboveFirstAsync(session.Page);
        await session.Page.Locator("#detach-dialog").WaitForAsync();
        var dialogText = await session.Page.Locator("#detach-dialog").InnerTextAsync();
        await session.Page.ClickAsync("#detach-cancel");
        await session.Page.Locator("#detach-dialog").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
        var order = await session.Page.EvaluateAsync<string>(TodoOrderScript);

        // Assert
        dialogText.Should().Contain("detached from its parent").And.Contain("standalone");
        order.Should().Be("alpha,beta,beta-child,gamma");
        ReorderBoardBuilder.TodoFiles(session.Board).Should().Equal(OriginalFiles);
    }

    [Fact]
    public async Task WarnMode_Continue_DetachesAndRenumbers()
    {
        // Arrange
        await using var session = await StartAsync(null);

        // Act
        await DragSubItemAboveFirstAsync(session.Page);
        await session.Page.Locator("#detach-dialog").WaitForAsync();
        await session.Page.ClickAsync("#detach-continue");
        await WaitForDetachedAsync(session.Page);

        // Assert
        ReorderBoardBuilder.TodoFiles(session.Board).Should().Equal(DetachedFiles);
    }

    [Fact]
    public async Task ForceMode_DetachesWithoutDialog()
    {
        // Arrange
        await using var session = await StartAsync("force");

        // Act
        await DragSubItemAboveFirstAsync(session.Page);
        await WaitForDetachedAsync(session.Page);
        var dialogCount = await session.Page.Locator("#detach-dialog").CountAsync();

        // Assert
        dialogCount.Should().Be(0);
        ReorderBoardBuilder.TodoFiles(session.Board).Should().Equal(DetachedFiles);
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs.Where(Directory.Exists))
        {
            Directory.Delete(dir, true);
        }
    }

    private const string TodoOrderScript = "() => Array.from(document.querySelectorAll('#col-Todo .card')).map(c => c.getAttribute('data-slug')).join(',')";

    private static async Task DragSubItemAboveFirstAsync(IPage page)
    {
        await page.Locator(SubItemCard).DragToAsync(
            page.Locator(FirstCard),
            new LocatorDragToOptions { TargetPosition = new TargetPosition { X = 10, Y = 2 } });
    }

    private static Task WaitForDetachedAsync(IPage page)
    {
        return page.WaitForFunctionAsync(
            "() => document.querySelector(\".card[data-slug='beta-child']\")?.getAttribute('data-id') === '1'");
    }

    private async Task<UiSession> StartAsync(string? mode)
    {
        var board = ReorderBoardBuilder.Create(_tempDirs, "ui-sub", mode, ReorderBoardBuilder.ParentAndSubItem);
        var server = await WebTestServer.StartAsync(_build.DllPath, board);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.GotoAsync(server.BaseUrl);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.card').length === 4");
        return new UiSession(server, playwright, browser, page, board);
    }

    private sealed class UiSession(WebTestServer server, IPlaywright playwright, IBrowser browser, IPage page, string board) : IAsyncDisposable
    {
        public IPage Page { get; } = page;

        public string Board { get; } = board;

        public async ValueTask DisposeAsync()
        {
            await browser.DisposeAsync();
            playwright.Dispose();
            await server.DisposeAsync();
        }
    }
}
