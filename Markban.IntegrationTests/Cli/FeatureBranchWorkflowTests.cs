using AwesomeAssertions;
using Markban.IntegrationTests.Infrastructure;
using Xunit;

namespace Markban.IntegrationTests;

[Collection("CLI")]
public class FeatureBranchWorkflowTests
{
    private const string BranchName = "feature/my-task";
    private const string EnabledSingle = """{"enabled":true,"commitStrategy":"single","prCommand":"echo pr-opened"}""";
    private const string ItemFile = "1-my-task.md";

    private readonly ToolBuildFixture _build;

    public FeatureBranchWorkflowTests(ToolBuildFixture build)
    {
        _build = build;
    }

    [Fact]
    public async Task Start_WhenDisabled_MovesItemWithoutCreatingBranch()
    {
        // Arrange
        using var repo = new GitTestRepo("""{"enabled":false}""");

        // Act
        var result = await RunAsync(repo, "start", "1");

        // Assert
        result.ExitCode.Should().Be(0, because: result.StdErr);
        repo.BoardFileExists("In Progress", ItemFile).Should().BeTrue();
        repo.CurrentBranch().Should().Be("main", because: "disabled mode must not create a branch");
    }

    [Fact]
    public async Task Start_WhenNoGitConfigPresent_MovesItemSilently()
    {
        // Arrange
        using var repo = new GitTestRepo("""{}""");

        // Act
        var result = await RunAsync(repo, "start", "my-task");

        // Assert
        result.ExitCode.Should().Be(0, because: result.StdErr);
        result.StdErr.Should().BeEmpty();
        repo.BoardFileExists("In Progress", ItemFile).Should().BeTrue();
    }

    [Fact]
    public async Task Start_WhenWorkingTreeDirty_FailsWithClearMessage()
    {
        // Arrange
        using var repo = new GitTestRepo(EnabledSingle);
        File.WriteAllText(Path.Combine(repo.RepoDir, "dirty.txt"), "x");

        // Act
        var result = await RunAsync(repo, "start", "1");

        // Assert
        result.StdErr.Should().Contain("uncommitted changes");
        repo.BoardFileExists("Todo", ItemFile).Should().BeTrue(because: "item must not move on failure");
        repo.CurrentBranch().Should().Be("main");
    }

    [Fact]
    public async Task Start_CreatesFeatureBranchAndMovesItemToInProgress()
    {
        // Arrange
        using var repo = new GitTestRepo(EnabledSingle);

        // Act
        var result = await RunAsync(repo, "start", "1");

        // Assert
        result.ExitCode.Should().Be(0, because: result.StdErr);
        repo.CurrentBranch().Should().Be(BranchName);
        repo.BoardFileExists("In Progress", ItemFile).Should().BeTrue();
        repo.BoardFileExists("Todo", ItemFile).Should().BeFalse();
    }

    [Fact]
    public async Task Start_PullsLatestMainBeforeBranching()
    {
        // Arrange
        using var repo = new GitTestRepo(EnabledSingle);
        repo.PushUpstreamCommit("main");

        // Act
        var result = await RunAsync(repo, "start", "1");

        // Assert
        result.ExitCode.Should().Be(0, because: result.StdErr);
        File.Exists(Path.Combine(repo.RepoDir, "upstream.txt")).Should().BeTrue(because: "main was pulled first");
    }

    [Fact]
    public async Task Start_WithNoPullFlag_SkipsPull()
    {
        // Arrange
        using var repo = new GitTestRepo(EnabledSingle);
        repo.BreakRemote();

        // Act
        var failing = await RunAsync(repo, "start", "1");
        var skipping = await RunAsync(repo, "start", "1", "--no-pull");

        // Assert
        failing.StdErr.Should().Contain("git pull failed", because: "the remote is unreachable");
        skipping.ExitCode.Should().Be(0, because: skipping.StdErr);
        repo.CurrentBranch().Should().Be(BranchName);
    }

    [Fact]
    public async Task Start_WithPullOnStartFalse_SkipsPull()
    {
        // Arrange
        using var repo = new GitTestRepo("""{"enabled":true,"pullOnStart":false}""");
        repo.BreakRemote();

        // Act
        var result = await RunAsync(repo, "start", "1");

        // Assert
        result.ExitCode.Should().Be(0, because: result.StdErr);
        repo.CurrentBranch().Should().Be(BranchName);
    }

    [Fact]
    public async Task Start_UsesConfiguredMainBranch()
    {
        // Arrange
        using var repo = new GitTestRepo("""{"enabled":true,"mainBranch":"trunk"}""", mainBranch: "trunk");

        // Act
        var result = await RunAsync(repo, "start", "1");

        // Assert
        result.ExitCode.Should().Be(0, because: result.StdErr);
        repo.CurrentBranch().Should().Be(BranchName);
        repo.CommitsAhead(BranchName, "trunk").Should().Be("0");
    }

    [Fact]
    public async Task Start_DryRun_ShowsPlannedCommandsAndChangesNothing()
    {
        // Arrange
        using var repo = new GitTestRepo(EnabledSingle);

        // Act
        var result = await RunAsync(repo, "start", "1", "--dry-run");

        // Assert
        result.StdOut.Should().Contain("git checkout main").And.Contain($"git checkout -b {BranchName}")
            .And.Contain("In Progress");
        repo.CurrentBranch().Should().Be("main");
        repo.BoardFileExists("Todo", ItemFile).Should().BeTrue();
    }

    [Fact]
    public async Task Commit_SingleStrategy_CommitsOnceOpensPrAndMovesToDone()
    {
        // Arrange
        using var repo = new GitTestRepo(EnabledSingle.Replace("\"enabled\":true", "\"enabled\":true,\"checkoutOnDone\":false"));
        await RunAsync(repo, "start", "1");

        // Act
        var result = await CommitAsync(repo);

        // Assert
        result.ExitCode.Should().Be(0, because: result.StdErr);
        result.StdOut.Should().Contain("pr-opened");
        repo.BoardFileExists("Done", ItemFile).Should().BeTrue();
        repo.CommitsAhead(BranchName, "main").Should().Be("1");
        repo.RemoteBranches().Should().Contain(BranchName);
    }

    [Fact]
    public async Task Commit_MultipleStrategy_KeepsExistingCommitsHistory()
    {
        // Arrange
        using var repo = new GitTestRepo("""{"enabled":true,"commitStrategy":"multiple","checkoutOnDone":false}""");
        await RunAsync(repo, "start", "1");
        repo.CommitFile("a.txt", "a", "feat: first");
        repo.CommitFile("b.txt", "b", "feat: second");

        // Act
        var result = await CommitAsync(repo);

        // Assert
        result.ExitCode.Should().Be(0, because: result.StdErr);
        repo.BoardFileExists("Done", ItemFile).Should().BeTrue();
        repo.CommitsAhead(BranchName, "main").Should().Be("3", because: "two user commits are preserved plus the board move");
        repo.RemoteBranches().Should().Contain(BranchName);
    }

    [Fact]
    public async Task Commit_SquashStrategy_SquashesBranchCommitsIntoOne()
    {
        // Arrange
        using var repo = new GitTestRepo("""{"enabled":true,"commitStrategy":"squash","checkoutOnDone":false}""");
        await RunAsync(repo, "start", "1");
        repo.CommitFile("a.txt", "a", "feat: first");
        repo.CommitFile("b.txt", "b", "feat: second");

        // Act
        var result = await CommitAsync(repo);

        // Assert
        result.ExitCode.Should().Be(0, because: result.StdErr);
        repo.CommitsAhead(BranchName, "main").Should().Be("1");
        repo.BoardFileExists("Done", ItemFile).Should().BeTrue();
        repo.Git(repo.RepoDir, "log", "-1", "--format=%s").Should().Be("feat: add feature");
    }

    [Fact]
    public async Task Commit_WithCheckoutOnDone_ReturnsToMainAndPulls()
    {
        // Arrange
        using var repo = new GitTestRepo(EnabledSingle);
        await RunAsync(repo, "start", "1");

        // Act
        var result = await CommitAsync(repo);

        // Assert
        result.ExitCode.Should().Be(0, because: result.StdErr);
        repo.CurrentBranch().Should().Be("main");
        repo.Status().Should().BeEmpty();
    }

    [Fact]
    public async Task Commit_WithCheckoutOnDoneFalse_StaysOnFeatureBranch()
    {
        // Arrange
        using var repo = new GitTestRepo("""{"enabled":true,"checkoutOnDone":false}""");
        await RunAsync(repo, "start", "1");

        // Act
        var result = await CommitAsync(repo);

        // Assert
        result.ExitCode.Should().Be(0, because: result.StdErr);
        repo.CurrentBranch().Should().Be(BranchName);
    }

    [Fact]
    public async Task Commit_DryRun_ShowsPlannedGitCommandsAndChangesNothing()
    {
        // Arrange
        using var repo = new GitTestRepo(EnabledSingle);
        await RunAsync(repo, "start", "1");

        // Act
        var result = await CommitAsync(repo, "--dry-run");

        // Assert
        result.StdOut.Should().Contain("-> Done").And.Contain("git commit").And.Contain("echo pr-opened")
            .And.Contain("git checkout main");
        repo.BoardFileExists("In Progress", ItemFile).Should().BeTrue();
        repo.CurrentBranch().Should().Be(BranchName);
    }

    [Fact]
    public async Task Init_WritesFeatureBranchDefaultsAsDisabled()
    {
        // Arrange
        string dir = Path.Combine(Path.GetTempPath(), "mb-init-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);

        try
        {
            // Act
            var result = await CliRunner.RunInDirAsync(_build.DllPath, dir, "init");

            // Assert
            result.ExitCode.Should().Be(0, because: result.StdErr);
            string config = File.ReadAllText(Path.Combine(dir, "markban.json"));
            config.Should().Contain("\"featureBranches\"").And.Contain("\"enabled\": false")
                .And.Contain("\"mainBranch\": \"main\"").And.Contain("\"commitStrategy\": \"single\"")
                .And.Contain("\"pullOnStart\": true").And.Contain("\"checkoutOnDone\": true");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public async Task Commit_SquashStrategy_WhenBranchAlreadyPushed_ForcePushesWithLease()
    {
        // Arrange
        using var repo = new GitTestRepo("""{"enabled":true,"commitStrategy":"squash","checkoutOnDone":false}""");
        await RunAsync(repo, "start", "1");
        repo.CommitFile("a.txt", "a", "feat: first");
        repo.Git(repo.RepoDir, "push", "-u", "origin", BranchName);
        repo.CommitFile("b.txt", "b", "feat: second");

        // Act
        var dryRun = await CommitAsync(repo, "--dry-run");
        var result = await CommitAsync(repo);

        // Assert
        dryRun.StdOut.Should().Contain("git push --force-with-lease");
        result.ExitCode.Should().Be(0, because: result.StdErr);
        result.StdOut.Should().Contain("git push --force-with-lease");
        repo.CommitsAhead(BranchName, "main").Should().Be("1");
        repo.Git(repo.RepoDir, "rev-parse", BranchName)
            .Should().Be(repo.Git(repo.RepoDir, "rev-parse", $"origin/{BranchName}"),
                because: "the squashed history replaced the previously pushed branch");
    }

    [Fact]
    public async Task Start_WhenItemUnknown_ExitsNonZero()
    {
        // Arrange
        using var repo = new GitTestRepo(EnabledSingle);

        // Act
        var result = await RunAsync(repo, "start", "does-not-exist");

        // Assert
        result.ExitCode.Should().NotBe(0);
        repo.CurrentBranch().Should().Be("main");
    }

    [Fact]
    public async Task Commit_WhenItemUnknown_ExitsNonZero()
    {
        // Arrange
        using var repo = new GitTestRepo(EnabledSingle);

        // Act
        var result = await RunAsync(repo, "commit", "nope", "--tag", "feat", "--message", "add feature");

        // Assert
        result.ExitCode.Should().NotBe(0);
    }

    private Task<CliResult> RunAsync(GitTestRepo repo, params string[] args)
        => CliRunner.RunAsync(_build.DllPath, repo.BoardRoot, args);

    private Task<CliResult> CommitAsync(GitTestRepo repo, params string[] extra)
        => RunAsync(repo, [.. new[] { "commit", "1", "--tag", "feat", "--message", "add feature" }, .. extra]);
}
