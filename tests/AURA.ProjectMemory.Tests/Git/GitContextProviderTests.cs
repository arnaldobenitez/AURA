using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Git;
using AURA.ProjectMemory.Tests.TestSupport;

namespace AURA.ProjectMemory.Tests.Git;

public class GitContextProviderTests
{
    [Fact]
    public void Capture_GitExecutableNotFound_IsUnavailable()
    {
        using var dir = new TempDirectory();
        var result = GitContextProvider.Capture(dir.Path, gitExecutable: "git-definitely-missing");

        Assert.Equal(GitAvailability.Unavailable, result.Availability);
        // Absence of the Git executable is the normal case for non-Git users and is surfaced as
        // INFO, never as an error (AURA_PROJECT_MEMORY v1.0 Etapa 6 §15/§16).
        Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.GitUnavailable, result.Diagnostics[0].Code);
    }

    [Fact]
    public void Capture_NotARepository_ReturnsNotRepository()
    {
        using var dir = new TempDirectory();
        var result = GitContextProvider.Capture(dir.Path);

        Assert.Equal(GitAvailability.NotRepository, result.Availability);
        // Being outside a repository is a normal, quiet state (AURA_PROJECT_MEMORY v1.0 Etapa 6 §16).
        Assert.Single(result.Diagnostics);
        Assert.Equal(DiagnosticCode.NotGitRepository, result.Diagnostics[0].Code);
    }

    [Fact]
    public void Capture_EmptyRepository_Clean_NoCommitsYet()
    {
        using var dir = new TempDirectory();
        GitTestHelper.InitRepository(dir.Path);

        var result = GitContextProvider.Capture(dir.Path);

        Assert.Equal(GitAvailability.Available, result.Availability);
        // No commits yet: HEAD has no branch to point to. This is not a detached HEAD, just empty.
        Assert.Null(result.Branch);
        Assert.Null(result.Commit);
        Assert.Equal(WorkingTreeStatus.Clean, result.WorkingTree);
        Assert.False(result.HasMergeConflicts);
    }

    [Fact]
    public void Capture_WithCommit_ReturnsCommitAndBranch()
    {
        using var dir = new TempDirectory();
        GitTestHelper.InitRepository(dir.Path);
        GitTestHelper.CommitFile(dir.Path, "a.txt", "first", "first");

        var result = GitContextProvider.Capture(dir.Path);

        Assert.Equal(GitAvailability.Available, result.Availability);
        Assert.Equal("master", result.Branch);
        Assert.NotNull(result.Commit);
        Assert.Equal(WorkingTreeStatus.Clean, result.WorkingTree);
    }

    [Fact]
    public void Capture_DirtyWorkingTree_Dirty()
    {
        using var dir = new TempDirectory();
        GitTestHelper.InitRepository(dir.Path);
        GitTestHelper.CommitFile(dir.Path, "a.txt", "first", "first");
        GitTestHelper.ModifyFile(dir.Path, "a.txt", "second");

        var result = GitContextProvider.Capture(dir.Path);

        Assert.Equal(WorkingTreeStatus.Dirty, result.WorkingTree);
    }

    [Fact]
    public void Capture_DetachedHead_BranchIsNull()
    {
        using var dir = new TempDirectory();
        GitTestHelper.InitRepository(dir.Path);
        var commit = GitTestHelper.CommitFile(dir.Path, "a.txt", "first", "first");

        GitTestHelper.RunGit(dir.Path, "checkout", "--detach", "HEAD");

        var result = GitContextProvider.Capture(dir.Path);

        Assert.Equal(GitAvailability.Available, result.Availability);
        Assert.Null(result.Branch);
        Assert.Equal(commit, result.Commit);
    }

    [Fact]
    public void Capture_PathWithSpaces_Works()
    {
        using var dir = new TempDirectory();
        // Force a directory name with spaces - TempDirectory uses a GUID root, but we create a
        // subfolder with spaces to prove ArgumentList handles the path correctly.
        var withSpaces = Path.Combine(dir.Path, "path with spaces");
        Directory.CreateDirectory(withSpaces);
        GitTestHelper.InitRepository(withSpaces);
        GitTestHelper.CommitFile(withSpaces, "a.txt", "x", "c");

        var result = GitContextProvider.Capture(withSpaces);

        Assert.Equal(GitAvailability.Available, result.Availability);
        Assert.NotNull(result.Commit);
        Assert.Equal(WorkingTreeStatus.Clean, result.WorkingTree);
    }

    [Fact]
    public void Capture_NestedRepo_UsesNearestRepo()
    {
        using var parent = new TempDirectory();
        GitTestHelper.InitRepository(parent.Path);
        GitTestHelper.CommitFile(parent.Path, "parent.txt", "p", "p");

        var nested = Path.Combine(parent.Path, "nested", "repo");
        Directory.CreateDirectory(nested);
        GitTestHelper.InitRepository(nested);
        var nestedCommit = GitTestHelper.CommitFile(nested, "nested.txt", "n", "n");

        var result = GitContextProvider.Capture(nested);

        Assert.Equal(GitAvailability.Available, result.Availability);
        Assert.Equal(nestedCommit, result.Commit);
    }

    [Fact]
    public void Capture_MergeConflict_HasConflicts()
    {
        using var dir = new TempDirectory();
        GitTestHelper.InitRepository(dir.Path);
        GitTestHelper.CommitFile(dir.Path, "a.txt", "base", "base");

        // Create a branch with a conflicting change.
        GitTestHelper.RunGit(dir.Path, "checkout", "-b", "feature");
        File.WriteAllText(Path.Combine(dir.Path, "a.txt"), "feature");
        GitTestHelper.RunGit(dir.Path, "add", "a.txt");
        GitTestHelper.RunGit(dir.Path, "commit", "-m", "feature");

        // Back to master with a different change.
        GitTestHelper.RunGit(dir.Path, "checkout", "master");
        File.WriteAllText(Path.Combine(dir.Path, "a.txt"), "master");
        GitTestHelper.RunGit(dir.Path, "add", "a.txt");
        GitTestHelper.RunGit(dir.Path, "commit", "-m", "master");

        // git merge exits with a non-zero code and leaves conflict markers when there are
        // unresolvable conflicts - this is expected and must not throw.
        GitTestHelper.RunGitRaw(dir.Path, "merge", "feature");

        var result = GitContextProvider.Capture(dir.Path);

        Assert.Equal(WorkingTreeStatus.Dirty, result.WorkingTree);
        Assert.True(result.HasMergeConflicts);
        Assert.Contains(result.Diagnostics, d => d.Code == "APM204");
    }
}
