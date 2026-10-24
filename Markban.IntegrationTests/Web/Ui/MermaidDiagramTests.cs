using System.Text;
using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class MermaidDiagramTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly List<string> _tempDirs = [];

    public MermaidDiagramTests(ToolBuildFixture build)
    {
        _build = build;
    }

    [Fact]
    public async Task ValidMermaidBlock_RendersAsSvg()
    {
        // Arrange
        var boardRoot = CreateBoard("mermaid-valid");
        AddItem(boardRoot, "Todo", "1-diagram-item.md", """
            # 1 - Diagram Item

            ## Description

            ```mermaid
            flowchart LR
                A --> B --> C
            ```
            """);
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        // Act
        await page.GotoAsync(server.BaseUrl);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.card').length === 1");
        await page.ClickAsync(".card[data-slug='diagram-item']");
        await page.Locator("#detail-pane.open").WaitForAsync();
        await page.WaitForFunctionAsync(
            "() => document.querySelector('#detail-content .mermaid svg') !== null",
            new PageWaitForFunctionOptions { Timeout = 15_000 });
        var svgCount = await page.Locator("#detail-content .mermaid svg").CountAsync();

        // Assert
        svgCount.Should().BeGreaterThan(0, because: "a valid mermaid code block should be rendered as an SVG diagram");
    }

    [Fact]
    public async Task InvalidMermaidBlock_ShowsInlineError_NotCrash()
    {
        // Arrange
        var boardRoot = CreateBoard("mermaid-err");
        AddItem(boardRoot, "Todo", "1-broken-diagram.md", """
            # 1 - Broken Diagram

            ## Description

            ```mermaid
            this is not valid mermaid @@@###!!!
            ```
            """);
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        // Act
        await page.GotoAsync(server.BaseUrl);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.card').length === 1");
        await page.ClickAsync(".card[data-slug='broken-diagram']");
        await page.Locator("#detail-pane.open").WaitForAsync();
        await page.WaitForFunctionAsync(
            "() => document.querySelector('#detail-content .mermaid-error') !== null",
            new PageWaitForFunctionOptions { Timeout = 15_000 });
        var errorCount = await page.Locator("#detail-content .mermaid-error").CountAsync();
        var detailPaneOpen = await page.Locator("#detail-pane.open").CountAsync();
        await page.Keyboard.PressAsync("Escape");
        await page.ClickAsync(".card[data-slug='broken-diagram']");
        await page.WaitForFunctionAsync("() => document.querySelector('#detail-content .mermaid-error') !== null");
        await page.Keyboard.PressAsync("Escape");
        var leakedSvgCount = await page.EvaluateAsync<int>(
            "() => [...document.querySelectorAll('svg')].filter(svg => !svg.closest('#detail-pane')).length");

        // Assert
        errorCount.Should().BeGreaterThan(0,
            because: "an invalid mermaid block should show a .mermaid-error element rather than crashing");
        detailPaneOpen.Should().Be(1,
            because: "a diagram parse error must not close or crash the detail pane");
        leakedSvgCount.Should().Be(0,
            because: "Mermaid error graphics must not leak onto the board after closing the detail pane");
    }

    [Fact]
    public async Task NonMermaidCodeBlock_StillRendersAsHighlightedPreCode()
    {
        // Arrange
        var boardRoot = CreateBoard("mermaid-regression");
        AddItem(boardRoot, "Todo", "1-mixed-item.md", """
            # 1 - Mixed Item

            ## Description

            ```mermaid
            flowchart LR
                X --> Y
            ```

            ```javascript
            console.log('hello');
            ```
            """);
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        // Act
        await page.GotoAsync(server.BaseUrl);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.card').length === 1");
        await page.ClickAsync(".card[data-slug='mixed-item']");
        await page.Locator("#detail-pane.open").WaitForAsync();
        await page.WaitForFunctionAsync(
            "() => document.querySelector('#detail-content .mermaid svg') !== null",
            new PageWaitForFunctionOptions { Timeout = 15_000 });
        var preCodeCount = await page.Locator("#detail-content pre code.language-javascript").CountAsync();

        // Assert
        preCodeCount.Should().Be(1,
            because: "a non-mermaid code block on the same page should still render as a highlighted <pre><code> element");
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
        File.WriteAllText(
            Path.Combine(parentDir, "markban.json"),
            $$"""{"rootPath":"{{rootForJson}}"}""",
            new UTF8Encoding(false));
        return boardRoot;
    }

    private static void AddItem(string boardRoot, string lane, string fileName, string content)
    {
        File.WriteAllText(Path.Combine(boardRoot, lane, fileName), content.ReplaceLineEndings("\n"), new UTF8Encoding(false));
    }
}
