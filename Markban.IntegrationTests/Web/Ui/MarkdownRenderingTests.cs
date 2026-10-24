using System.Text;
using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class MarkdownRenderingTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly List<string> _tempDirs = [];

    public MarkdownRenderingTests(ToolBuildFixture build)
    {
        _build = build;
    }

    [Fact]
    public async Task GfmTable_RendersAsHtmlTable()
    {
        // Arrange
        var boardRoot = CreateBoard("md-table");
        AddItem(boardRoot, "Todo", "1-table-item.md", """
            # 1 - Table Item

            ## Description

            | Column A | Column B |
            |----------|----------|
            | Cell 1   | Cell 2   |
            | Cell 3   | Cell 4   |
            """);
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        // Act
        await page.GotoAsync(server.BaseUrl);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.card').length === 1");
        await page.ClickAsync(".card[data-slug='table-item']");
        await page.Locator("#detail-pane.open").WaitForAsync();
        await page.WaitForFunctionAsync("() => document.querySelector('#detail-content table') !== null");
        var tableCount = await page.Locator("#detail-content table").CountAsync();

        // Assert
        tableCount.Should().BeGreaterThan(0, because: "a GFM table in the markdown should render as an HTML <table> element");
    }

    [Fact]
    public async Task FencedCodeBlock_RendersWithLanguageHighlightClass()
    {
        // Arrange
        var boardRoot = CreateBoard("md-code");
        AddItem(boardRoot, "Todo", "1-code-item.md", """
            # 1 - Code Item

            ## Description

            ```csharp
            var x = 1 + 2;
            Console.WriteLine(x);
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
        await page.ClickAsync(".card[data-slug='code-item']");
        await page.Locator("#detail-pane.open").WaitForAsync();
        await page.WaitForFunctionAsync("() => document.querySelector('#detail-content pre code') !== null");
        var codeElement = page.Locator("#detail-content pre code");

        // Assert
        var className = await codeElement.GetAttributeAsync("class");
        className.Should().Contain("language-csharp",
            because: "a fenced code block with a language tag should render with a language-specific CSS class");
    }

    [Fact]
    public async Task TaskListItems_RenderAsCheckboxInputs()
    {
        // Arrange
        var boardRoot = CreateBoard("md-tasklist");
        AddItem(boardRoot, "Todo", "1-tasks-item.md", """
            # 1 - Tasks Item

            ## Acceptance Criteria

            - [x] Done task
            - [ ] Pending task
            """);
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        // Act
        await page.GotoAsync(server.BaseUrl);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.card').length === 1");
        await page.ClickAsync(".card[data-slug='tasks-item']");
        await page.Locator("#detail-pane.open").WaitForAsync();
        await page.WaitForFunctionAsync("() => document.querySelector('#detail-content input[type=\"checkbox\"]') !== null");
        var checkboxCount = await page.Locator("#detail-content input[type='checkbox']").CountAsync();

        // Assert
        checkboxCount.Should().Be(2,
            because: "both task list items (checked and unchecked) should render as <input type='checkbox'> elements");
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
