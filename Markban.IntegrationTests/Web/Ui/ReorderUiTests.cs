using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class ReorderUiTests : IDisposable
{
    private const string SecondCard = ".card[data-slug='beta']";
    private const string FirstCard = ".card[data-slug='alpha']";
    private const string TodoSlugsScript = "() => Array.from(document.querySelectorAll('#col-Todo .card')).map(c => c.getAttribute('data-slug')).join(',')";

    private readonly ToolBuildFixture _build;
    private readonly List<string> _tempDirs = [];

    public ReorderUiTests(ToolBuildFixture build)
    {
        _build = build;
    }

    [Fact]
    public async Task DragSecondCardAboveFirst_ReordersDom()
    {
        // Arrange
        var boardRoot = CreateBoard("ui-reorder-dom");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);
        await using var browser = await LaunchBrowserAsync();
        var page = await OpenBoardAsync(browser, server);

        // Act
        await DragSecondAboveFirstAsync(page);
        await page.WaitForFunctionAsync($"() => ({TodoSlugsScript.Substring(6)}) === 'beta,alpha,gamma'");
        var order = await page.EvaluateAsync<string>(TodoSlugsScript);

        // Assert
        order.Should().Be("beta,alpha,gamma");
    }

    [Fact]
    public async Task DragDrop_PersistsNewOrder_VisibleViaGetItems()
    {
        // Arrange
        var boardRoot = CreateBoard("ui-reorder-persist");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);
        await using var browser = await LaunchBrowserAsync();
        var page = await OpenBoardAsync(browser, server);

        // Act
        await DragSecondAboveFirstAsync(page);
        await page.WaitForFunctionAsync($"() => ({TodoSlugsScript.Substring(6)}) === 'beta,alpha,gamma' && document.querySelector(\"{FirstCard}\").getAttribute('data-id') === '2'");
        var items = await server.Client.GetFromJsonAsync<JsonElement>("/api/items");
        var todoSlugs = items.EnumerateArray()
            .Where(i => i.GetProperty("status").GetString() == "Todo")
            .OrderBy(i => int.Parse(i.GetProperty("id").GetString()!))
            .Select(i => i.GetProperty("slug").GetString())
            .ToArray();

        // Assert
        todoSlugs.Should().Equal("beta", "alpha", "gamma");
    }

    [Fact]
    public async Task FailedReorder_RestoresOriginalOrder_AndShowsError()
    {
        // Arrange
        var boardRoot = CreateBoard("ui-reorder-fail");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);
        await using var browser = await LaunchBrowserAsync();
        var page = await OpenBoardAsync(browser, server);
        await page.RouteAsync("**/api/reorder*", route => route.FulfillAsync(new RouteFulfillOptions
        {
            Status = 500,
            ContentType = "application/json",
            Body = "{\"error\":\"boom\"}"
        }));

        // Act
        await DragSecondAboveFirstAsync(page);
        await page.Locator("#reorder-error").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var order = await page.EvaluateAsync<string>(TodoSlugsScript);
        var errorText = await page.Locator("#reorder-error").InnerTextAsync();

        // Assert
        order.Should().Be("alpha,beta,gamma", because: "a failed server call must restore the original order");
        errorText.Should().Contain("boom");
    }

    [Fact]
    public async Task DropIndicator_AboveFirstCard_OpensGapWithMarkerAboveIt()
    {
        // Arrange
        var boardRoot = CreateBoard("ui-marker-first");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);
        await using var browser = await LaunchBrowserAsync();
        var page = await OpenBoardAsync(browser, server);

        // Act
        var state = await HoverDraggedAsync(page, "gamma", "#col-Todo", "top");

        // Assert
        state.MarkerVisible.Should().BeTrue();
        state.ShiftedCards.Should().Be(2, because: "both neighbours slide down to open the gap");
        state.MarkerCenter.Should().BeInRange(state.FirstOtherCardTop, state.FirstOtherCardTop + 36, because: "the marker sits in the opened gap above the first card");
    }

    [Fact]
    public async Task DropIndicator_BelowLastCard_ShowsMarkerBelowIt()
    {
        // Arrange
        var boardRoot = CreateBoard("ui-marker-last");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);
        await using var browser = await LaunchBrowserAsync();
        var page = await OpenBoardAsync(browser, server);

        // Act
        var state = await HoverDraggedAsync(page, "alpha", "#col-Todo", "bottom");

        // Assert
        state.MarkerVisible.Should().BeTrue();
        state.ShiftedCards.Should().Be(0);
        state.MarkerCenter.Should().BeGreaterThan(state.LastOtherCardBottom);
    }

    [Fact]
    public async Task DropIndicator_InEmptyLane_ShowsMarker_AndClearsOnDragEnd()
    {
        // Arrange
        var boardRoot = CreateBoard("ui-marker-empty");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);
        await using var browser = await LaunchBrowserAsync();
        var page = await OpenBoardAsync(browser, server);

        // Act
        var state = await HoverDraggedAsync(page, "alpha", "#col-Testing", "top");
        await page.EvaluateAsync("() => document.querySelector(\".card[data-slug='alpha']\").dispatchEvent(new DragEvent('dragend', { bubbles: true }))");
        var visibleAfterEnd = await page.Locator(".drop-marker.visible").CountAsync();

        // Assert
        state.MarkerVisible.Should().BeTrue();
        visibleAfterEnd.Should().Be(0);
    }

    [Fact]
    public async Task DraggedCard_CollapsesOutOfFlow_NeighboursCloseUp_AndRestoresOnDragEnd()
    {
        // Arrange
        var boardRoot = CreateBoard("ui-collapse");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);
        await using var browser = await LaunchBrowserAsync();
        var page = await OpenBoardAsync(browser, server);
        var originalGammaTop = await page.EvaluateAsync<double>("() => document.querySelector(\".card[data-slug='gamma']\").offsetTop");

        // Act
        await StartDragAsync(page, "alpha");
        var collapsedGammaTop = await page.EvaluateAsync<double>("() => document.querySelector(\".card[data-slug='gamma']\").offsetTop");
        var kept = await page.Locator(".card[data-slug='alpha']").CountAsync();
        await page.EvaluateAsync("() => document.querySelector(\".card[data-slug='alpha']\").dispatchEvent(new DragEvent('dragend', { bubbles: true }))");
        await page.WaitForFunctionAsync("() => { const c = document.querySelector(\".card[data-slug='alpha']\"); return !c.classList.contains('dragging') && c.style.height === ''; }");
        var restoredGammaTop = await page.EvaluateAsync<double>("() => document.querySelector(\".card[data-slug='gamma']\").offsetTop");

        // Assert
        kept.Should().Be(1, because: "the original element must stay in the DOM so dragend still fires");
        collapsedGammaTop.Should().BeLessThan(originalGammaTop, because: "neighbours close up over the collapsed card");
        restoredGammaTop.Should().Be(originalGammaTop);
    }

    [Fact]
    public async Task DroppingAtOriginalPosition_IsNoOp_AndSendsNoRequest()
    {
        // Arrange
        var boardRoot = CreateBoard("ui-noop");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);
        await using var browser = await LaunchBrowserAsync();
        var page = await OpenBoardAsync(browser, server);
        var requests = 0;
        await page.RouteAsync("**/api/reorder*", route =>
        {
            requests++;
            return route.ContinueAsync();
        });

        // Act
        await StartDragAsync(page, "beta");
        await page.EvaluateAsync("""
            () => {
                const zone = document.querySelector('#col-Todo');
                const alpha = document.querySelector(".card[data-slug='alpha']").getBoundingClientRect();
                const init = { bubbles: true, cancelable: true, dataTransfer: window.__dt, clientY: alpha.bottom + 4 };
                zone.dispatchEvent(new DragEvent('dragover', init));
                zone.dispatchEvent(new DragEvent('drop', init));
                document.querySelector(".card[data-slug='beta']").dispatchEvent(new DragEvent('dragend', { bubbles: true }));
            }
            """);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.drop-marker.visible').length === 0");
        var order = await page.EvaluateAsync<string>(TodoSlugsScript);

        // Assert
        order.Should().Be("alpha,beta,gamma");
        requests.Should().Be(0, because: "dropping where the card already was changes nothing");
    }

    public void Dispose()
    {
        foreach (var dir in _tempDirs.Where(Directory.Exists))
        {
            Directory.Delete(dir, true);
        }
    }

    private sealed record MarkerState(bool MarkerVisible, int ShiftedCards, double MarkerCenter, double FirstOtherCardTop, double LastOtherCardBottom);

    private const string HoverScript = """
        ([slug, zoneSel, where]) => {
            const dt = window.__dt;
            const card = document.querySelector(`.card[data-slug='${slug}']`);
            const zone = document.querySelector(zoneSel);
            const r = zone.getBoundingClientRect();
            const y = where === 'top' ? r.top + 1 : r.bottom - 1;
            zone.dispatchEvent(new DragEvent('dragover', { bubbles: true, cancelable: true, dataTransfer: dt, clientY: y }));
            const marker = zone.querySelector('.drop-marker.visible');
            const others = Array.from(zone.querySelectorAll('.card')).filter(c => c !== card);
            return {
                markerVisible: !!marker,
                shiftedCards: zone.querySelectorAll('.card.drop-shift').length,
                markerCenter: marker ? marker.offsetTop + marker.offsetHeight / 2 : 0,
                firstOtherCardTop: others.length ? others[0].offsetTop : 0,
                lastOtherCardBottom: others.length ? others[others.length - 1].offsetTop + others[others.length - 1].offsetHeight : 0
            };
        }
        """;

    private const string StartDragScript = """
        slug => {
            window.__dt = new DataTransfer();
            const card = document.querySelector(`.card[data-slug='${slug}']`);
            card.dispatchEvent(new DragEvent('dragstart', { bubbles: true, dataTransfer: window.__dt }));
        }
        """;

    private static async Task StartDragAsync(IPage page, string slug)
    {
        await page.EvaluateAsync(StartDragScript, slug);
        await page.WaitForFunctionAsync("s => { const c = document.querySelector('.card[data-slug=\"' + s + '\"]'); return c.classList.contains('dragging') && c.getBoundingClientRect().height === 0; }", slug);
    }

    private static async Task<MarkerState> HoverDraggedAsync(IPage page, string slug, string zone, string where)
    {
        await StartDragAsync(page, slug);
        var json = await page.EvaluateAsync<JsonElement>(HoverScript, new object[] { slug, zone, where });
        return new MarkerState(
            json.GetProperty("markerVisible").GetBoolean(),
            json.GetProperty("shiftedCards").GetInt32(),
            json.GetProperty("markerCenter").GetDouble(),
            json.GetProperty("firstOtherCardTop").GetDouble(),
            json.GetProperty("lastOtherCardBottom").GetDouble());
    }

    private static async Task DragSecondAboveFirstAsync(IPage page)
    {
        await page.Locator(SecondCard).DragToAsync(
            page.Locator(FirstCard),
            new LocatorDragToOptions { TargetPosition = new TargetPosition { X = 10, Y = 2 } });
    }

    private static async Task<IPage> OpenBoardAsync(IBrowser browser, WebTestServer server)
    {
        var page = await browser.NewPageAsync();
        await page.GotoAsync(server.BaseUrl);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.card').length === 3");
        return page;
    }

    private static async Task<IBrowser> LaunchBrowserAsync()
    {
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        var playwright = await Playwright.CreateAsync();
        return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
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
        File.WriteAllText(Path.Combine(parentDir, "markban.json"), $$"""{"rootPath":"{{rootForJson}}"}""", new UTF8Encoding(false));
        foreach (var (id, slug) in new[] { (1, "alpha"), (2, "beta"), (3, "gamma") })
        {
            File.WriteAllText(Path.Combine(boardRoot, "Todo", $"{id}-{slug}.md"), $"# {id} - {slug}\n", new UTF8Encoding(false));
        }

        return boardRoot;
    }
}
