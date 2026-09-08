using System.Security.Cryptography;
using System.Text.Json;
using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Git;
using AURA.ProjectMemory.Initialization;
using AURA.ProjectMemory.Session;
using AURA.ProjectMemory.Tests.TestSupport;
using AURA.ProjectMemory.Validation;

namespace AURA.ProjectMemory.Tests.Session;

public class SessionGitAndPortabilityTests
{
    private static SessionStartRequest MakeStartRequest(string? machineProfilePath = null, string? slug = null) =>
        new("Implement multi-pc support", "coding-agent", "Arnaldo", "Claude", "test-model", slug, machineProfilePath);

    private static SessionCloseRequest MakeCloseRequest(Action<SessionCloseRequest>? modify = null)
    {
        var r = new SessionCloseRequest("Done", "Nothing", "Move on");
        modify?.Invoke(r);
        return r;
    }

    [Fact]
    public void Start_CapturesGitContext_WhenInsideRepository()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path);
        GitTestHelper.InitRepository(project.Path);
        GitTestHelper.CommitFile(project.Path, "readme.md", "ok", "initial");
        GitTestHelper.ModifyFile(project.Path, "readme.md", "dirty");

        var result = SessionManager.Start(project.Path, MakeStartRequest(), runtime.Path);

        Assert.True(result.Success);
        var status = SessionManager.Status(project.Path, runtime.Path);
        var session = status.OpenSessions.Single();
        Assert.Equal("master", session.GitBranch);
        Assert.NotNull(session.InitialCommit);
        Assert.Equal("dirty", session.WorkingTreeInitial);
    }

    [Fact]
    public void Start_WithoutGit_NoGitFieldsCaptured()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path);

        var result = SessionManager.Start(project.Path, MakeStartRequest(), runtime.Path);

        Assert.True(result.Success);
        var status = SessionManager.Status(project.Path, runtime.Path);
        var session = status.OpenSessions.Single();
        Assert.Null(session.GitBranch);
        Assert.Null(session.InitialCommit);
        Assert.Null(session.WorkingTreeInitial);
    }

    [Fact]
    public void Close_CapturesFinalGitContext_AndInitialFromState()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path);
        GitTestHelper.InitRepository(project.Path);
        File.WriteAllText(Path.Combine(project.Path, "readme.md"), "ok");
        GitTestHelper.RunGit(project.Path, "add", ".");
        GitTestHelper.RunGit(project.Path, "commit", "-m", "initial");
        var initial = GitTestHelper.GetCommitHash(project.Path);

        var start = SessionManager.Start(project.Path, MakeStartRequest(), runtime.Path);
        File.WriteAllText(Path.Combine(project.Path, "second.md"), "x");
        GitTestHelper.RunGit(project.Path, "add", ".");
        GitTestHelper.RunGit(project.Path, "commit", "-m", "second");

        var close = SessionManager.Close(project.Path, start.SessionId!,
            MakeCloseRequest(), runtime.Path);

        Assert.True(close.Success);
        var history = File.ReadAllText(project.Combine(close.HistoryFile!.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains($"`initialCommit` | `{initial}`", history);
        Assert.Contains($"`finalCommit` | `{GitTestHelper.GetCommitHash(project.Path)}`", history);
        Assert.Contains($"`workingTreeFinal` | `clean`", history);
    }

    [Fact]
    public void Close_WithoutFinalCommit_WorkingTreeDirtyCaptured()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path);
        GitTestHelper.InitRepository(project.Path);
        var initial = GitTestHelper.CommitFile(project.Path, "readme.md", "ok", "initial");

        var start = SessionManager.Start(project.Path, MakeStartRequest(), runtime.Path);
        GitTestHelper.ModifyFile(project.Path, "readme.md", "dirty");

        var close = SessionManager.Close(project.Path, start.SessionId!,
            MakeCloseRequest(), runtime.Path);

        Assert.True(close.Success);
        var history = File.ReadAllText(project.Combine(close.HistoryFile!.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains($"`initialCommit` | `{initial}`", history);
        Assert.Contains($"`finalCommit` | `{initial}`", history);
        Assert.Contains($"`workingTreeFinal` | `dirty`", history);
    }

    [Fact]
    public void Close_ManualGitOverride_DisagreementProducesWarning_AndAutoWins()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path);
        GitTestHelper.InitRepository(project.Path);
        var initial = GitTestHelper.CommitFile(project.Path, "readme.md", "ok", "initial");

        var start = SessionManager.Start(project.Path, MakeStartRequest(), runtime.Path);

        var close = SessionManager.Close(project.Path, start.SessionId!,
            new SessionCloseRequest("Done", "Nothing", "Move on",
                InitialCommit: "manual-bad",
                FinalCommit: "manual-bad",
                WorkingTreeFinal: "manual-bad",
                GitBranch: "manual-bad"),
            runtime.Path);

        Assert.True(close.Success);
        Assert.Contains(close.Diagnostics, d => d.Code == DiagnosticCode.GitMetadataMismatch && d.Severity == DiagnosticSeverity.Warning);

        var history = File.ReadAllText(project.Combine(close.HistoryFile!.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Contains($"`initialCommit` | `{initial}`", history);
        Assert.DoesNotContain("`initialCommit` | `manual-bad`", history);
    }

    [Fact]
    public void FullMultiPcScenario_TwoMachinesSameProject_DifferentMachineIds_BothHistories()
    {
        using var projectA = new TempDirectory();
        using var machineA = new TempDirectory();
        using var machineB = new TempDirectory();
        using var runtimeA = new TempDirectory();
        using var runtimeB = new TempDirectory();

        // Machine A.
        var machineAInit = MachineProfileInitializer.Init(machineA.Combine("machine.json"));
        Assert.True(machineAInit.Success);

        ProjectMemoryInitializer.Init(projectA.Path, "Multi PC Project");
        var startA = SessionManager.Start(projectA.Path,
            MakeStartRequest(machineA.Combine("machine.json"), "machine-a"), runtimeA.Path);
        var closeA = SessionManager.Close(projectA.Path, startA.SessionId!,
            MakeCloseRequest(), runtimeA.Path);
        Assert.True(closeA.Success);

        // Copy project to "Machine B" (different directory, different machine profile).
        var projectB = Path.Combine(projectA.Path, "..", "project-b"); // ensure distinct path
        projectB = Path.GetFullPath(projectB);
        CopyDirectory(projectA.Path, projectB);

        var machineBInit = MachineProfileInitializer.Init(machineB.Combine("machine.json"));

        // Validate B - same projectId.
        var validationB = ProjectMemoryValidator.Validate(projectB,
            machineProfilePath: machineB.Combine("machine.json"));
        Assert.True(validationB.IsValid);

        var startB = SessionManager.Start(projectB,
            MakeStartRequest(machineB.Combine("machine.json"), "machine-b"), runtimeB.Path);
        var closeB = SessionManager.Close(projectB, startB.SessionId!,
            MakeCloseRequest(), runtimeB.Path);
        Assert.True(closeB.Success);

        // Both histories exist in B (project is now the copied directory).
        var historyAInB = projectB + Path.DirectorySeparatorChar + closeA.HistoryFile!.Replace('/', Path.DirectorySeparatorChar);
        var historyB = projectB + Path.DirectorySeparatorChar + closeB.HistoryFile!.Replace('/', Path.DirectorySeparatorChar);

        Assert.True(File.Exists(historyAInB));
        Assert.True(File.Exists(historyB));

        // Compare projectId (should be equal) and machineId (should differ).
        var sourceValidation = ProjectMemoryValidator.Validate(projectA.Path);
        Assert.Equal(sourceValidation.ProjectId, validationB.ProjectId);

        var contentA = File.ReadAllText(historyAInB);
        var contentB = File.ReadAllText(historyB);
        Assert.Contains($"`machineId` | `{machineAInit.MachineId}`", contentA);
        Assert.Contains($"`machineId` | `{machineBInit.MachineId}`", contentB);
        Assert.NotEqual(machineAInit.MachineId, machineBInit.MachineId);
    }

    [Fact]
    public void RenameFolder_DoesNotChangeProjectId()
    {
        using var parent = new TempDirectory();
        using var runtime = new TempDirectory();
        var original = Path.Combine(parent.Path, "OriginalName");
        Directory.CreateDirectory(original);
        var init = ProjectMemoryInitializer.Init(original, "Renamed Project");

        var renamed = Path.Combine(parent.Path, "CompletelyDifferentName");
        Directory.Move(original, renamed);

        var validation = ProjectMemoryValidator.Validate(renamed);
        Assert.True(validation.IsValid);
        Assert.Equal(init.ProjectId, validation.ProjectId);

        var start = SessionManager.Start(renamed, MakeStartRequest(), runtime.Path);
        var close = SessionManager.Close(renamed, start.SessionId!,
            MakeCloseRequest(), runtime.Path);
        Assert.True(close.Success);
    }

    [Fact]
    public void ProjectContext_ReportsGitAndMachineInformation()
    {
        using var project = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path, "Context Project");
        GitTestHelper.InitRepository(project.Path);
        GitTestHelper.CommitFile(project.Path, "a.txt", "x", "first");

        var context = ProjectContextService.Get(project.Path);

        Assert.True(context.Project.IsValid);
        Assert.NotNull(context.Project.ProjectId);
        Assert.Equal(GitAvailability.Available, context.Git.Availability);
        Assert.NotNull(context.Git.Commit);
        Assert.Equal("master", context.Git.Branch);
    }

    [Fact]
    public void ProjectContext_WithoutGit_StillReportsProjectInfo()
    {
        using var project = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path, "No Git Context");

        var context = ProjectContextService.Get(project.Path);

        Assert.True(context.Project.IsValid);
        Assert.Equal(GitAvailability.NotRepository, context.Git.Availability);
    }

    [Fact]
    public void PathDifference_DoesNotAffectIdentity()
    {
        using var source = new TempDirectory();
        using var destParent = new TempDirectory();
        using var runtime = new TempDirectory();
        var init = ProjectMemoryInitializer.Init(source.Path, "Path Diff");

        var destination = Path.Combine(destParent.Path, "very", "different", "path", "project");
        CopyDirectory(source.Path, destination);

        var validation = ProjectMemoryValidator.Validate(destination);
        Assert.True(validation.IsValid);
        Assert.Equal(init.ProjectId, validation.ProjectId);

        var start = SessionManager.Start(destination, MakeStartRequest(), runtime.Path);
        var close = SessionManager.Close(destination, start.SessionId!,
            MakeCloseRequest(), runtime.Path);
        Assert.True(close.Success);
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectory(subDir, Path.Combine(destinationDir, Path.GetFileName(subDir)));
        }
    }
}
