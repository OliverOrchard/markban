using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class ThemeTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly TestWorkspace _workspace = new();

    public ThemeTests(ToolBuildFixture build)
    {
        _build = build;
        _workspace.AddItem("Todo", "1-theme-demo.md", """
            # 1 - Theme demo

            ```javascript
            const message = "hello";
            ```

            ```mermaid
            flowchart LR
                A --> B
            ```
            """);
    }

    [Theory]
    [InlineData(ColorScheme.Dark, "dark", "rgb(21, 21, 21)")]
    [InlineData(ColorScheme.Light, "light", "rgb(247, 247, 255)")]
    public async Task SystemTheme_FollowsDeviceAppearance(ColorScheme scheme, string theme, string background)
    {
        // Arrange
        await using var server = await WebTestServer.StartAsync(_build.DllPath, _workspace.Root);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync(new() { ColorScheme = scheme });

        // Act
        await page.GotoAsync(server.BaseUrl);
        await page.Locator(".card").WaitForAsync();
        string? preference = await page.Locator("#theme-select").InputValueAsync();
        string? effectiveTheme = await page.Locator("html").GetAttributeAsync("data-theme");
        string actualBackground = await page.EvaluateAsync<string>("getComputedStyle(document.body).backgroundColor");
        var opposite = scheme == ColorScheme.Dark ? ColorScheme.Light : ColorScheme.Dark;
        await page.EmulateMediaAsync(new() { ColorScheme = opposite });
        string expectedTheme = theme == "dark" ? "light" : "dark";
        await page.WaitForFunctionAsync("theme => document.documentElement.dataset.theme === theme", expectedTheme);

        // Assert
        preference.Should().Be("system");
        effectiveTheme.Should().Be(theme);
        actualBackground.Should().Be(background);
        (await page.Locator("#theme-select").InputValueAsync()).Should().Be("system");
    }

    [Theory]
    [InlineData("dark", ColorScheme.Light)]
    [InlineData("light", ColorScheme.Dark)]
    public async Task ExplicitTheme_PersistsAcrossReload_AndOverridesSystem(string theme, ColorScheme systemScheme)
    {
        // Arrange
        await using var server = await WebTestServer.StartAsync(_build.DllPath, _workspace.Root);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync(new() { ColorScheme = systemScheme });

        // Act
        await page.GotoAsync(server.BaseUrl);
        await page.Locator(".card").WaitForAsync();
        await page.SelectOptionAsync("#theme-select", theme);
        await page.EmulateMediaAsync(new() { ColorScheme = systemScheme == ColorScheme.Dark ? ColorScheme.Light : ColorScheme.Dark });
        await page.EmulateMediaAsync(new() { ColorScheme = systemScheme });
        string? beforeReload = await page.Locator("html").GetAttributeAsync("data-theme");
        await page.ReloadAsync();
        await page.Locator(".card").WaitForAsync();

        // Assert
        beforeReload.Should().Be(theme, because: "explicit choices must ignore device theme changes");
        (await page.Locator("html").GetAttributeAsync("data-theme")).Should().Be(theme);
        (await page.Locator("#theme-select").InputValueAsync()).Should().Be(theme);
        (await page.EvaluateAsync<string>("localStorage.getItem('markban-theme')")).Should().Be(theme);
    }

    [Fact]
    public async Task DarkTheme_UsesBronzeForActions_AndNeutralLaneFilters()
    {
        // Arrange
        _workspace.AddItem("Todo", "1-theme-demo.md", "---\ntags: [review]\n---\n# 1 - Theme demo");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, _workspace.Root);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync(new() { ColorScheme = ColorScheme.Dark });
        await page.GotoAsync(server.BaseUrl);
        var filter = page.Locator(".filter-btn[data-lane='Todo']");
        await filter.WaitForAsync();

        // Act
        string[] colors = await page.EvaluateAsync<string[]>("""
            () => {
                const filter = getComputedStyle(document.querySelector('.filter-btn.active'));
                return [
                    filter.color, filter.backgroundColor, filter.borderColor,
                    getComputedStyle(document.querySelector('.card-id')).color,
                    getComputedStyle(document.querySelector('.badge-tag')).color,
                    getComputedStyle(document.querySelector('.move-btn')).backgroundColor
                ];
            }
            """);
        await filter.ClickAsync();
        string? hiddenState = await filter.GetAttributeAsync("aria-pressed");
        bool columnHidden = await page.Locator(".column[data-lane='Todo']").IsHiddenAsync();
        string decoration = await filter.EvaluateAsync<string>("button => getComputedStyle(button).textDecorationLine");
        await filter.ClickAsync();

        // Assert
        colors.Should().Equal("rgb(247, 247, 255)", "rgb(32, 32, 39)", "rgb(53, 53, 63)",
            "rgb(170, 170, 184)", "rgb(170, 170, 184)", "rgb(230, 180, 80)");
        hiddenState.Should().Be("false");
        columnHidden.Should().BeTrue();
        decoration.Should().Be("line-through", because: "hidden lanes must not be distinguished by color alone");
        (await filter.GetAttributeAsync("aria-pressed")).Should().Be("true");
        (await page.Locator(".column[data-lane='Todo']").IsVisibleAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task LightTheme_KeepsExistingTealPaletteAndFilters()
    {
        // Arrange
        await using var server = await WebTestServer.StartAsync(_build.DllPath, _workspace.Root);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync(new() { ColorScheme = ColorScheme.Light });

        // Act
        await page.GotoAsync(server.BaseUrl);
        await page.Locator(".filter-btn.active").First.WaitForAsync();
        string[] colors = await page.EvaluateAsync<string[]>("""
            () => {
                const filter = getComputedStyle(document.querySelector('.filter-btn.active'));
                return [
                    getComputedStyle(document.body).backgroundColor,
                    filter.color, filter.backgroundColor, filter.borderColor,
                    getComputedStyle(document.querySelector('.card-id')).color,
                    getComputedStyle(document.querySelector('.move-btn')).backgroundColor
                ];
            }
            """);

        // Assert
        colors.Should().Equal("rgb(247, 247, 255)", "rgb(0, 123, 131)", "rgba(0, 123, 131, 0.09)",
            "rgb(0, 123, 131)", "rgb(0, 123, 131)", "rgb(0, 123, 131)");
    }

    [Fact]
    public async Task HighContrastMode_KeepsNativeScrollbars()
    {
        // Arrange
        await using var server = await WebTestServer.StartAsync(_build.DllPath, _workspace.Root);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync(new() { ForcedColors = ForcedColors.Active });

        // Act
        await page.GotoAsync(server.BaseUrl);
        await page.Locator(".card").WaitForAsync();
        string[] styles = await page.Locator("#kanban").EvaluateAsync<string[]>("""
            element => {
                const style = getComputedStyle(element);
                return [style.scrollbarWidth, style.scrollbarColor];
            }
            """);

        // Assert
        styles.Should().Equal(["auto", "auto"], because: "high-contrast mode must retain native scrollbar styling");
    }

    [Theory]
    [InlineData(ColorScheme.Dark, "rgb(119, 119, 132)", "rgb(170, 170, 184)", "rgb(24, 24, 26)")]
    [InlineData(ColorScheme.Light, "rgb(133, 133, 148)", "rgb(90, 90, 107)", "rgb(238, 240, 245)")]
    public async Task Scrollbars_MatchTheme_AcrossBoardDetailCodeAndEditor(
        ColorScheme scheme, string thumbColor, string hoverColor, string trackColor)
    {
        // Arrange
        _workspace.AddItem("Todo", "1-theme-demo.md", "# 1 - Theme demo\n\n```text\nA code block\n```");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, _workspace.Root);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync(new() { ColorScheme = scheme });
        await page.GotoAsync(server.BaseUrl);

        // Act
        await page.Locator(".card").ClickAsync();
        await page.Locator("#detail-content pre").WaitForAsync();
        string[][] styles = await page.EvaluateAsync<string[][]>("""
            () => ['html', '.kanban', '.cards', '#detail-pane', '#detail-content pre', '#editor-textarea']
                .map(selector => {
                    const element = document.querySelector(selector);
                    const thumb = getComputedStyle(element, '::-webkit-scrollbar-thumb');
                    const track = getComputedStyle(element, '::-webkit-scrollbar-track');
                    const bar = getComputedStyle(element, '::-webkit-scrollbar');
                    return [thumb.backgroundColor, track.backgroundColor, thumb.borderRadius, bar.width, bar.height];
                })
            """);
        int horizontalScroll = await page.Locator("#kanban").EvaluateAsync<int>(
            "element => { element.scrollLeft = 80; return element.scrollLeft; }");

        // Assert
        foreach (string[] style in styles)
        {
            style[0].Should().BeOneOf([thumbColor, hoverColor],
                because: "both normal and hovered scrollbar thumbs must use the theme's neutral colors");
            style.Skip(1).Should().Equal(trackColor, "999px", "12px", "12px");
        }

        horizontalScroll.Should().BeGreaterThan(0, because: "custom styling must preserve horizontal board scrolling");
    }

    [Fact]
    public async Task ThemeChange_RerendersDiagramsAndCode_WithoutLosingEditorDraft()
    {
        // Arrange
        await using var server = await WebTestServer.StartAsync(_build.DllPath, _workspace.Root);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync(new() { ColorScheme = ColorScheme.Dark });
        await page.GotoAsync(server.BaseUrl);
        await page.Locator(".card").ClickAsync();
        await page.Locator("#detail-content .mermaid svg").WaitForAsync();
        string? originalSvg = await page.Locator("#detail-content .mermaid svg").GetAttributeAsync("id");
        string originalNodeFill = await page.Locator("#detail-content .mermaid .node rect")
            .First.EvaluateAsync<string>("node => getComputedStyle(node).fill");
        string originalCodeColor = await page.Locator(".hljs-keyword")
            .First.EvaluateAsync<string>("node => getComputedStyle(node).color");

        // Act
        await page.SelectOptionAsync("select[aria-label='Detail theme']", "light");
        await page.WaitForFunctionAsync(
            "id => { const svg = document.querySelector('#detail-content .mermaid svg'); return svg && svg.id !== id; }", originalSvg);
        string lightNodeFill = await page.Locator("#detail-content .mermaid .node rect")
            .First.EvaluateAsync<string>("node => getComputedStyle(node).fill");
        string lightCodeColor = await page.Locator(".hljs-keyword")
            .First.EvaluateAsync<string>("node => getComputedStyle(node).color");
        await page.ClickAsync("#edit-btn");
        await page.FillAsync("#editor-textarea", "Unsaved theme-switch draft");
        await page.SelectOptionAsync("select[aria-label='Detail theme']", "dark");

        // Assert
        lightNodeFill.Should().NotBe(originalNodeFill, because: "Mermaid SVG styles must follow the selected theme");
        lightCodeColor.Should().NotBe(originalCodeColor, because: "code highlighting must follow the selected theme");
        (await page.Locator("#editor-textarea").InputValueAsync()).Should().Be("Unsaved theme-switch draft");
        (await page.Locator("#detail-editor").IsVisibleAsync()).Should().BeTrue();
        (await page.Locator("#theme-select").InputValueAsync()).Should().Be("dark");
    }

    [Fact]
    public async Task UnavailableStorage_StillAppliesTheme_AndExplainsPersistenceFailure()
    {
        // Arrange
        await using var server = await WebTestServer.StartAsync(_build.DllPath, _workspace.Root);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        await page.AddInitScriptAsync("""
            Storage.prototype.getItem = () => { throw new Error('Storage disabled'); };
            Storage.prototype.setItem = () => { throw new Error('Storage disabled'); };
            """);

        // Act
        await page.GotoAsync(server.BaseUrl);
        await page.Locator(".card").WaitForAsync();
        await page.SelectOptionAsync("#theme-select", "dark");

        // Assert
        (await page.Locator("html").GetAttributeAsync("data-theme")).Should().Be("dark");
        (await page.Locator("#theme-feedback").InnerTextAsync()).Should().Contain("could not save");
        (await page.Locator("#theme-feedback").IsVisibleAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task ThemePreference_IsSharedAcrossTabs_AndSystemCanBeRestored()
    {
        // Arrange
        await using var server = await WebTestServer.StartAsync(_build.DllPath, _workspace.Root);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        await using var context = await browser.NewContextAsync(new() { ColorScheme = ColorScheme.Light });
        var first = await context.NewPageAsync();
        var second = await context.NewPageAsync();
        await first.GotoAsync(server.BaseUrl);
        await second.GotoAsync(server.BaseUrl);
        await first.Locator(".card").WaitForAsync();
        await second.Locator(".card").WaitForAsync();

        // Act
        await first.SelectOptionAsync("#theme-select", "dark");
        await second.WaitForFunctionAsync("() => document.documentElement.dataset.theme === 'dark'");
        string syncedPreference = await second.Locator("#theme-select").InputValueAsync();
        await second.SelectOptionAsync("#theme-select", "system");
        await first.WaitForFunctionAsync("() => document.documentElement.dataset.theme === 'light'");

        // Assert
        syncedPreference.Should().Be("dark");
        (await first.Locator("#theme-select").InputValueAsync()).Should().Be("system");
        (await first.EvaluateAsync<string>("localStorage.getItem('markban-theme')")).Should().Be("system");
    }

    [Fact]
    public async Task CycleTimeReport_UsesSavedTheme_AndRedrawsChartWhenChanged()
    {
        // Arrange
        await using var server = await WebTestServer.StartAsync(_build.DllPath, _workspace.Root);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var page = await browser.NewPageAsync();
        await page.RouteAsync("**/api/reports/cycle-time", route => route.FulfillAsync(new()
        {
            ContentType = "application/json",
            Body = """[{"id":"1","slug":"theme-demo","startedDate":"2026-10-01","completedDate":"2026-10-03","daysInProgress":2}]"""
        }));
        await page.GotoAsync(server.BaseUrl);
        await page.Locator(".card").WaitForAsync();
        await page.SelectOptionAsync("#theme-select", "dark");

        // Act
        await page.ClickAsync(".reports-link");
        await page.Locator("#table").WaitForAsync();
        string savedTheme = await page.Locator("#theme-select").InputValueAsync();
        int[] darkPixel = await ReadChartBackgroundAsync(page);
        await page.SelectOptionAsync("#theme-select", "light");
        int[] lightPixel = await ReadChartBackgroundAsync(page);

        // Assert
        savedTheme.Should().Be("dark");
        darkPixel.Should().Equal(26, 26, 31, 255);
        lightPixel.Should().Equal(255, 255, 255, 255);
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private static Task<int[]> ReadChartBackgroundAsync(IPage page)
    {
        return page.EvaluateAsync<int[]>(
            "() => [...document.querySelector('#scatter-canvas').getContext('2d').getImageData(0, 0, 1, 1).data]");
    }
}
