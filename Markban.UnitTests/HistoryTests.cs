using System.Diagnostics;
using System.Text;
using AwesomeAssertions;
using Xunit;

namespace Markban.UnitTests;

public class HistoryTests : IDisposable
{
    private readonly List<string> _tempDirs = [];

    public void Dispose()
    {
        foreach (string dir in _tempDirs.Where(Directory.Exists))
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Reorder_WritesJournalEntry_OutsideLanes_WithBeforeAndAfterHashes()
    {
        // Arrange
        string root = CreateBoard(
            items: [("1-alpha.md", "# 1 - Alpha\n\nBody A"), ("2-beta.md", "# 2 - Beta\n\nBody B")]);

        // Act
        ReorderCommand.Execute(root, "Todo", "2,1", noSubItems: false, dryRun: false);
        var load = HistoryStore.Load(root);

        // Assert
        string journalPath = HistoryStore.GetJournalPath(root);
        File.Exists(journalPath).Should().BeTrue(because: "a successful reorder must persist the journal");
        Path.GetDirectoryName(journalPath)!.Should().EndWith(".markban");
        load.IsCorrupt.Should().BeFalse();
        load.Journal.Undo.Should().ContainSingle();
        var entry = load.Journal.Undo[0];
        entry.Operation.Should().Be(HistoryOperations.Reorder);
        entry.Changes.Should().HaveCount(2);
        entry.Changes[0].Before.Path.Should().Be("Todo/2-beta.md");
        entry.Changes[0].Before.Number.Should().Be("2");
        entry.Changes[0].After.Path.Should().Be("Todo/1-beta.md");
        entry.Changes[0].After.Number.Should().Be("1");
        entry.Changes[0].After.Hash.Should().Be(
            HistoryContentRestorer.ComputeFileHash(Path.Combine(root, "Todo", "1-beta.md")));
    }

    [Fact]
    public void Reorder_DryRun_OrInvalidOrder_DoesNotWriteJournalEntry()
    {
        // Arrange
        string root = CreateBoard(
            items: [("1-alpha.md", "# 1 - Alpha\n"), ("2-beta.md", "# 2 - Beta\n")]);

        // Act
        ReorderCommand.Execute(root, "Todo", "2,1", noSubItems: false, dryRun: true);
        ReorderCommand.Execute(root, "Todo", "99,1", noSubItems: false, dryRun: false);
        var load = HistoryStore.Load(root);

        // Assert
        load.Journal.Undo.Should().BeEmpty(because: "dry-runs and failed reorders must not record entries");
    }

    [Fact]
    public void Move_WritesJournalEntry_AndUndoRestoresOriginalLaneAndNumber()
    {
        // Arrange
        string root = CreateBoard(
            items: [("1-alpha.md", "# 1 - Alpha\n"), ("2-beta.md", "# 2 - Beta\n"), ("3-gamma.md", "# 3 - Gamma\n")]);

        // Act
        MoveCommand.Execute(root, "2", "Ideas");
        var afterMove = HistoryStore.Load(root);
        var undoResult = UndoCommand.Execute(root);

        // Assert
        afterMove.Journal.Undo.Should().ContainSingle()
            .Which.Operation.Should().Be(HistoryOperations.Move);
        undoResult.IsSuccess.Should().BeTrue(because: undoResult.Message);
        File.Exists(Path.Combine(root, "Todo", "2-beta.md")).Should().BeTrue();
        File.Exists(Path.Combine(root, "Todo", "3-gamma.md")).Should().BeTrue();
        File.Exists(Path.Combine(root, "Ideas", "beta.md")).Should().BeFalse();
    }

    [Fact]
    public void Undo_ThenRedo_RoundTripsFilesHeadingsAndStacks_WithoutRecordingNewEntries()
    {
        // Arrange
        string root = CreateBoard(
            items: [("1-alpha.md", "# 1 - Alpha\n"), ("2-beta.md", "# 2 - Beta\n")]);
        ReorderCommand.Execute(root, "Todo", "2,1", noSubItems: false, dryRun: false);
        string entryId = HistoryStore.Load(root).Journal.Undo[0].Id;

        // Act
        var undoResult = UndoCommand.Execute(root);
        var afterUndo = HistoryStore.Load(root).Journal;
        var redoResult = RedoCommand.Execute(root);
        var afterRedo = HistoryStore.Load(root).Journal;

        // Assert
        undoResult.IsSuccess.Should().BeTrue(because: undoResult.Message);
        afterUndo.Undo.Should().BeEmpty();
        afterUndo.Redo.Should().ContainSingle().Which.Id.Should().Be(entryId);
        redoResult.IsSuccess.Should().BeTrue(because: redoResult.Message);
        afterRedo.Undo.Should().ContainSingle().Which.Id.Should().Be(entryId);
        afterRedo.Redo.Should().BeEmpty();
        File.ReadAllText(Path.Combine(root, "Todo", "1-beta.md")).Should().Contain("# 1 - Beta");
        File.ReadAllText(Path.Combine(root, "Todo", "2-alpha.md")).Should().Contain("# 2 - Alpha");
    }

    [Fact]
    public void NewOperation_AfterUndo_ClearsRedoStack()
    {
        // Arrange
        string root = CreateBoard(
            items: [("1-alpha.md", "# 1 - Alpha\n"), ("2-beta.md", "# 2 - Beta\n")]);
        ReorderCommand.Execute(root, "Todo", "2,1", noSubItems: false, dryRun: false);
        UndoCommand.Execute(root);

        // Act
        MoveCommand.Execute(root, "1", "In Progress");
        var journal = HistoryStore.Load(root).Journal;

        // Assert
        journal.Undo.Should().ContainSingle().Which.Operation.Should().Be(HistoryOperations.Move);
        journal.Redo.Should().BeEmpty(because: "a new recorded operation must clear the redo stack");
    }

    [Fact]
    public void MaxEntries_DropsOldestEntries_WhenLimitExceeded()
    {
        // Arrange
        string root = CreateBoard(
            extraConfigJson: "\"history\": { \"enabled\": true, \"maxEntries\": 2 }",
            items: [("1-alpha.md", "# 1 - Alpha\n"), ("2-beta.md", "# 2 - Beta\n"), ("3-gamma.md", "# 3 - Gamma\n")]);

        // Act
        ReorderCommand.Execute(root, "Todo", "2,1,3", noSubItems: false, dryRun: false);
        ReorderCommand.Execute(root, "Todo", "3,1,2", noSubItems: false, dryRun: false);
        MoveCommand.Execute(root, "1", "In Progress");
        var journal = HistoryStore.Load(root).Journal;

        // Assert
        journal.Undo.Should().HaveCount(2, because: "history.maxEntries is 2 so the oldest entry must be dropped");
        journal.Undo[0].Description.Should().Be("reorder Todo 3,1,2");
        journal.Undo[1].Operation.Should().Be(HistoryOperations.Move);
    }

    [Fact]
    public void Undo_WhenAffectedFileWasEdited_RefusesNamesFileAndChangesNothing()
    {
        // Arrange
        string root = CreateBoard(
            items: [("1-alpha.md", "# 1 - Alpha\n"), ("2-beta.md", "# 2 - Beta\n")]);
        ReorderCommand.Execute(root, "Todo", "2,1", noSubItems: false, dryRun: false);
        string editedPath = Path.Combine(root, "Todo", "1-beta.md");
        File.AppendAllText(editedPath, "\nLater edit by someone else\n");

        // Act
        var result = UndoCommand.Execute(root);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Todo/1-beta.md");
        File.ReadAllText(editedPath).Should().Contain("Later edit by someone else");
        File.Exists(Path.Combine(root, "Todo", "2-alpha.md")).Should().BeTrue();
        HistoryStore.Load(root).Journal.Undo.Should().ContainSingle(
            because: "a refused undo must leave the journal untouched");
    }

    [Fact]
    public void Redo_WhenAffectedFileWasDeleted_RefusesNamesFileAndChangesNothing()
    {
        // Arrange
        string root = CreateBoard(
            items: [("1-alpha.md", "# 1 - Alpha\n"), ("2-beta.md", "# 2 - Beta\n")]);
        ReorderCommand.Execute(root, "Todo", "2,1", noSubItems: false, dryRun: false);
        UndoCommand.Execute(root);
        File.Delete(Path.Combine(root, "Todo", "2-beta.md"));

        // Act
        var result = RedoCommand.Execute(root);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("Todo/2-beta.md");
        File.Exists(Path.Combine(root, "Todo", "1-alpha.md")).Should().BeTrue();
        HistoryStore.Load(root).Journal.Redo.Should().ContainSingle();
    }

    [Fact]
    public void Undo_ForceSubItemReorder_ReattachesSubItemToOriginalParent()
    {
        // Arrange
        string root = CreateBoard(
            extraConfigJson: "\"subItemReorder\": \"force\"",
            items:
            [
                ("1-alpha.md", "# 1 - alpha\n"),
                ("2-beta.md", "# 2 - beta\n"),
                ("2a-beta-child.md", "# 2a - beta-child\n"),
                ("3-gamma.md", "# 3 - gamma\n")
            ]);
        var options = new SubItemReorderOptions(DryRun: false, AssumeYes: false, TextReader.Null, InputIsInteractive: false);
        SubItemReorderCommand.Execute(root, "Todo", "2a,1,2,3", options);

        // Act
        var undoResult = UndoCommand.Execute(root);

        // Assert
        undoResult.IsSuccess.Should().BeTrue(because: undoResult.Message);
        var files = Directory.GetFiles(Path.Combine(root, "Todo"), "*.md").Select(Path.GetFileName).Order().ToArray();
        files.Should().Equal("1-alpha.md", "2-beta.md", "2a-beta-child.md", "3-gamma.md");
        File.ReadAllText(Path.Combine(root, "Todo", "2a-beta-child.md")).Should().Contain("# 2a - beta-child");
    }

    [Fact]
    public void CorruptJournal_IsReportedClearly_AndDoesNotBlockNormalReorder()
    {
        // Arrange
        string root = CreateBoard(
            items: [("1-alpha.md", "# 1 - Alpha\n"), ("2-beta.md", "# 2 - Beta\n")]);
        string journalPath = HistoryStore.GetJournalPath(root);
        Directory.CreateDirectory(Path.GetDirectoryName(journalPath)!);
        File.WriteAllText(journalPath, "{ corrupt-json");

        // Act
        var undoOnCorrupt = UndoCommand.Execute(root);
        ReorderCommand.Execute(root, "Todo", "2,1", noSubItems: false, dryRun: false);
        var afterReorder = HistoryStore.Load(root);

        // Assert
        undoOnCorrupt.IsSuccess.Should().BeFalse();
        undoOnCorrupt.Message.Should().Contain("corrupt or unreadable");
        File.Exists(Path.Combine(root, "Todo", "1-beta.md")).Should().BeTrue(
            because: "a corrupt journal must never block normal commands");
        afterReorder.IsCorrupt.Should().BeFalse();
        afterReorder.Journal.Undo.Should().ContainSingle();
    }

    [Fact]
    public void HistoryDisabled_RecordsNothing_AndCommandsExplainWhy()
    {
        // Arrange
        string root = CreateBoard(
            extraConfigJson: "\"history\": { \"enabled\": false, \"maxEntries\": 50 }",
            items: [("1-alpha.md", "# 1 - Alpha\n"), ("2-beta.md", "# 2 - Beta\n")]);

        // Act
        ReorderCommand.Execute(root, "Todo", "2,1", noSubItems: false, dryRun: false);
        var undoResult = UndoCommand.Execute(root);
        var redoResult = RedoCommand.Execute(root);
        var listResult = HistoryListCommand.Execute(root);

        // Assert
        File.Exists(HistoryStore.GetJournalPath(root)).Should().BeFalse(
            because: "nothing should be recorded when history.enabled is false");
        undoResult.IsSuccess.Should().BeFalse();
        undoResult.Message.Should().Contain("disabled");
        redoResult.IsSuccess.Should().BeFalse();
        redoResult.Message.Should().Contain("disabled");
        listResult.Message.Should().Contain("disabled");
    }

    [Fact]
    public void InitCommand_WritesHistoryDefaults_AndLoadSettingsParsesThem()
    {
        // Arrange
        string dir = Path.Combine(Path.GetTempPath(), "mb-hist-init-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);

        // Act
        InitCommand.Execute(dir, boardPath: null, name: null, dryRun: false);
        string root = Path.Combine(dir, "work-items");
        var settings = WorkItemStore.LoadSettings(root);

        // Assert
        string configText = File.ReadAllText(Path.Combine(dir, "markban.json"));
        configText.Should().Contain("\"history\"");
        configText.Should().Contain("\"maxEntries\": 50");
        settings.History.Should().NotBeNull();
        settings.History!.Enabled.Should().BeTrue();
        settings.History.MaxEntries.Should().Be(50);
    }

    [Fact]
    public void UndoAndRedo_DryRun_AndList_WorkWithoutMutatingState()
    {
        // Arrange
        string root = CreateBoard(
            items: [("1-alpha.md", "# 1 - Alpha\n"), ("2-beta.md", "# 2 - Beta\n")]);
        ReorderCommand.Execute(root, "Todo", "2,1", noSubItems: false, dryRun: false);

        // Act
        var dryUndo = UndoCommand.Execute(root, dryRun: true);
        var listResult = HistoryListCommand.Execute(root);

        // Assert
        dryUndo.IsSuccess.Should().BeTrue();
        dryUndo.Message.Should().Contain("Dry run").And.Contain("Todo/1-beta.md -> Todo/2-beta.md");
        File.Exists(Path.Combine(root, "Todo", "1-beta.md")).Should().BeTrue(
            because: "dry-run must not rename files");
        listResult.IsSuccess.Should().BeTrue();
        listResult.Message.Should().Contain("Undo stack (1)").And.Contain("Redo stack (0)");
    }

    [Fact]
    public void Suppress_PreventsMoveAndReorder_FromWritingJournalEntries()
    {
        // Arrange
        string root = CreateBoard(
            items: [("1-alpha.md", "# 1 - Alpha\n"), ("2-beta.md", "# 2 - Beta\n")]);

        // Act
        using (HistoryRecorder.Suppress())
        {
            ReorderCommand.Execute(root, "Todo", "2,1", noSubItems: false, dryRun: false);
            MoveCommand.Execute(root, "1", "In Progress");
        }

        var load = HistoryStore.Load(root);

        // Assert
        File.Exists(Path.Combine(root, "In Progress", "1-beta.md")).Should().BeTrue();
        load.Journal.Undo.Should().BeEmpty(
            because: "suppressed internal move and reorder calls must not write journal entries");
        File.Exists(HistoryStore.GetJournalPath(root)).Should().BeFalse();
    }

    [Fact]
    public void Reorder_InGitRepo_CreatesSelfIgnoringMarkbanDir_NotListedByGitStatus()
    {
        // Arrange
        string root = CreateBoard(
            items: [("1-alpha.md", "# 1 - Alpha\n"), ("2-beta.md", "# 2 - Beta\n")]);
        string repoDir = Path.GetDirectoryName(root)!;
        RunGit(repoDir, "init");

        // Act
        ReorderCommand.Execute(root, "Todo", "2,1", noSubItems: false, dryRun: false);
        string statusOutput = RunGit(repoDir, "status", "--porcelain");

        // Assert
        string gitignorePath = Path.Combine(repoDir, ".markban", ".gitignore");
        File.Exists(gitignorePath).Should().BeTrue(
            because: "HistoryStore must write .markban/.gitignore so git ignores the journal automatically");
        File.ReadAllText(gitignorePath).Trim().Should().Be("*");
        statusOutput.Should().NotContain(".markban",
            because: "the self-ignoring .markban directory must not appear in git status --porcelain");
    }

    private static string RunGit(string workingDir, params string[] args)
    {
        var psi = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (string arg in args)
        {
            psi.ArgumentList.Add(arg);
        }

        using var proc = Process.Start(psi)!;
        string stdout = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit();
        return stdout;
    }

    private string CreateBoard(
        string? extraConfigJson = null,
        params (string FileName, string Content)[] items)
    {
        string parentDir = Path.Combine(Path.GetTempPath(), "mb-hist-" + Guid.NewGuid().ToString("N")[..8]);
        string boardRoot = Path.Combine(parentDir, "work-items");
        _tempDirs.Add(parentDir);

        foreach (string lane in new[] { "Todo", "In Progress", "Testing", "Done", "Ideas", "Rejected" })
        {
            Directory.CreateDirectory(Path.Combine(boardRoot, lane));
        }

        string rootForJson = boardRoot.Replace("\\", "/");
        string extra = extraConfigJson is null ? string.Empty : $",\n  {extraConfigJson}";
        string json = $$"""
            {
              "rootPath": "{{rootForJson}}"{{extra}}
            }
            """;
        File.WriteAllText(Path.Combine(parentDir, "markban.json"), json, new UTF8Encoding(false));

        foreach (var (fileName, content) in items)
        {
            File.WriteAllText(Path.Combine(boardRoot, "Todo", fileName), content, new UTF8Encoding(false));
        }

        return boardRoot;
    }
}
