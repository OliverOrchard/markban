using System.Text;
using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Microsoft.Playwright;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class EditWorkItemUiTests : IDisposable
{
    private readonly ToolBuildFixture _build;
    private readonly List<string> _tempDirs = [];

    public EditWorkItemUiTests(ToolBuildFixture build)
    {
        _build = build;
    }

    [Fact]
    public async Task EditButton_OpensTextarea_PrepopulatedWithFileContent()
    {
        // Arrange
        const string originalContent = "# 1 - Edit Me\n\n## Description\n\nOriginal body text.";
        var boardRoot = CreateBoard("edit-open");
        AddItem(boardRoot, "Todo", "1-edit-me.md", originalContent);
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        // Act
        await page.GotoAsync(server.BaseUrl);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.card').length === 1");
        await page.ClickAsync(".card[data-slug='edit-me']");
        await page.Locator("#detail-pane.open").WaitForAsync();
        await page.ClickAsync("#edit-btn");
        await page.Locator("#detail-editor").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        var textareaValue = await page.Locator("#editor-textarea").InputValueAsync();
        var isContentHidden = await page.Locator("#detail-content").IsHiddenAsync();

        // Assert
        textareaValue.Should().Contain("Original body text.",
            because: "the editor textarea must be pre-populated with the current file content");
        isContentHidden.Should().BeTrue(
            because: "the rendered view should be hidden while the editor is open");
    }

    [Fact]
    public async Task SaveEdit_UpdatesRenderedView_WithoutFullPageReload()
    {
        // Arrange
        var boardRoot = CreateBoard("edit-save");
        AddItem(boardRoot, "Todo", "1-save-me.md", "# 1 - Save Me\n\n## Description\n\nBefore save.");
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        // Act
        await page.GotoAsync(server.BaseUrl);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.card').length === 1");
        await page.EvaluateAsync("window.__loadMarker = Date.now()");
        await page.ClickAsync(".card[data-slug='save-me']");
        await page.Locator("#detail-pane.open").WaitForAsync();
        await page.ClickAsync("#edit-btn");
        await page.Locator("#detail-editor").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.FillAsync("#editor-textarea", "# 1 - Save Me\n\n## Description\n\nAfter save.");
        await page.ClickAsync("#save-btn");
        await page.WaitForFunctionAsync(
            "() => document.querySelector('#detail-content')?.style.display !== 'none' && document.querySelector('#detail-content')?.innerText.includes('After save.')");
        var markerStillSet = await page.EvaluateAsync<bool>("() => typeof window.__loadMarker !== 'undefined'");
        var diskContent = File.ReadAllText(Path.Combine(boardRoot, "Todo", "1-save-me.md"));

        // Assert
        markerStillSet.Should().BeTrue(
            because: "saving should update the view in-place without a full page reload");
        diskContent.Should().Contain("After save.",
            because: "the save API must persist the updated content to the file on disk");
    }

    [Fact]
    public async Task CancelEdit_ClosesEditor_LeavesContentUnchanged()
    {
        // Arrange
        const string originalContent = "# 1 - Cancel Me\n\n## Description\n\nDo not change this.";
        var boardRoot = CreateBoard("edit-cancel");
        AddItem(boardRoot, "Todo", "1-cancel-me.md", originalContent);
        await using var server = await WebTestServer.StartAsync(_build.DllPath, boardRoot);
        await PlaywrightInstaller.EnsureChromiumInstalledAsync();
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();

        // Act
        await page.GotoAsync(server.BaseUrl);
        await page.WaitForFunctionAsync("() => document.querySelectorAll('.card').length === 1");
        await page.ClickAsync(".card[data-slug='cancel-me']");
        await page.Locator("#detail-pane.open").WaitForAsync();
        await page.ClickAsync("#edit-btn");
        await page.Locator("#detail-editor").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });
        await page.FillAsync("#editor-textarea", "# 1 - Cancel Me\n\nThis should not be saved.");
        await page.ClickAsync("#cancel-btn");
        await page.Locator("#detail-editor").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Hidden });
        var isEditorHidden = await page.Locator("#detail-editor").IsHiddenAsync();
        var isContentVisible = await page.Locator("#detail-content").IsVisibleAsync();
        var diskContent = File.ReadAllText(Path.Combine(boardRoot, "Todo", "1-cancel-me.md"));

        // Assert
        isEditorHidden.Should().BeTrue(because: "Cancel must close the editor");
        isContentVisible.Should().BeTrue(because: "the rendered view should be restored after Cancel");
        diskContent.Should().Be(originalContent,
            because: "cancelling must not write any changes to disk");
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
        File.WriteAllText(Path.Combine(boardRoot, lane, fileName), content, new UTF8Encoding(false));
    }
}
