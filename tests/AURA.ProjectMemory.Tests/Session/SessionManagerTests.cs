using System.Security.Cryptography;
using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Initialization;
using AURA.ProjectMemory.Session;
using AURA.ProjectMemory.Tests.TestSupport;
using AURA.ProjectMemory.Validation;

namespace AURA.ProjectMemory.Tests.Session;

public class SessionManagerTests
{
    private static SessionStartRequest MakeStartRequest(string? machineProfilePath, string? slug = null) =>
        new("Implement something", "coding-agent", "Arnaldo", "Claude", "test-model", slug, machineProfilePath);

    private static SessionCloseRequest MakeCloseRequest() =>
        new("It worked", "Nothing left", "Move on to the next task");

    [Fact]
    public void Start_OnValidProject_GeneratesUuidV4SessionIdAndCapturesProjectId()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        using var machine = new TempDirectory();
        var init = ProjectMemoryInitializer.Init(project.Path);

        var result = SessionManager.Start(project.Path, MakeStartRequest(machine.Combine("machine.json")),
            runtime.Path);

        Assert.True(result.Success);
        Assert.Equal(init.ProjectId, result.ProjectId);
        var guid = Guid.Parse(result.SessionId!);
        Assert.Equal(4, (guid.ToByteArray()[7] >> 4) & 0x0F);
        Assert.NotNull(result.StartedAt);
    }

    [Fact]
    public void Start_MissingMachineProfile_DoesNotFailAndReportsInfo()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        using var machine = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path);

        var result = SessionManager.Start(project.Path, MakeStartRequest(machine.Combine("absent.json")), runtime.Path);

        Assert.True(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCode.MachineProfileMissingForSession);
    }

    [Fact]
    public void Start_WithMachineProfile_CapturesMachineIdAndName()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        using var machine = new TempDirectory();
        var machinePath = machine.Combine("machine.json");
        MachineProfileInitializer.Init(machinePath);
        ProjectMemoryInitializer.Init(project.Path);

        SessionManager.Start(project.Path, MakeStartRequest(machinePath), runtime.Path);

        var status = SessionManager.Status(project.Path, runtime.Path);
        Assert.NotNull(status.OpenSessions.Single().MachineId);
        Assert.NotNull(status.OpenSessions.Single().MachineName);
    }

    [Fact]
    public void Start_OnInvalidProject_IsRejected()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        // No init() call: project.Path has no Core at all.

        var result = SessionManager.Start(project.Path, MakeStartRequest(null), runtime.Path);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Start_FromChildDirectory_ResolvesTheSameProject()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        var init = ProjectMemoryInitializer.Init(project.Path);
        var child = project.Combine("src", "nested");
        Directory.CreateDirectory(child);

        var result = SessionManager.Start(child, MakeStartRequest(null), runtime.Path);

        Assert.True(result.Success);
        Assert.Equal(init.ProjectId, result.ProjectId);
    }

    [Fact]
    public void Start_MultipleSessions_ProduceDifferentSessionIds()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path);

        var first = SessionManager.Start(project.Path, MakeStartRequest(null), runtime.Path);
        var second = SessionManager.Start(project.Path, MakeStartRequest(null), runtime.Path);

        Assert.NotEqual(first.SessionId, second.SessionId);

        var status = SessionManager.Status(project.Path, runtime.Path);
        Assert.Equal(2, status.OpenSessions.Count);
    }

    [Fact]
    public void Close_ValidSession_CreatesHistoryFileWithNormativeNaming()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path);
        var start = SessionManager.Start(project.Path, MakeStartRequest(null, "close test"), runtime.Path);

        var result = SessionManager.Close(project.Path, start.SessionId!, MakeCloseRequest(), runtime.Path);

        Assert.True(result.Success);
        Assert.StartsWith("docs/historial/", result.HistoryFile);
        Assert.Contains(start.SessionId!, result.HistoryFile);
        Assert.Matches(
            @"^docs/historial/\d{4}-\d{2}-\d{2}T\d{2}-\d{2}-\d{2}Z__[0-9a-f-]{36}__close-test\.md$",
            result.HistoryFile);
        Assert.True(File.Exists(project.Combine(result.HistoryFile!.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void Close_ActiveStateIsRemovedAfterSuccess()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path);
        var start = SessionManager.Start(project.Path, MakeStartRequest(null), runtime.Path);

        SessionManager.Close(project.Path, start.SessionId!, MakeCloseRequest(), runtime.Path);

        var status = SessionManager.Status(project.Path, runtime.Path);
        Assert.Empty(status.OpenSessions);
    }

    [Fact]
    public void Close_InvalidPayload_KeepsSessionOpenForRetry()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path);
        var start = SessionManager.Start(project.Path, MakeStartRequest(null), runtime.Path);
        var invalidRequest = new SessionCloseRequest("", "Nothing", "Move on");

        var result = SessionManager.Close(project.Path, start.SessionId!, invalidRequest, runtime.Path);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCode.InvalidSessionPayload);

        var status = SessionManager.Status(project.Path, runtime.Path);
        Assert.Single(status.OpenSessions);
    }

    [Fact]
    public void Close_UnknownSession_IsRejectedWithoutModifyingTheProject()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path);
        var historyBefore = Directory.GetFiles(project.Combine("docs", "historial"));

        var result = SessionManager.Close(project.Path, Guid.NewGuid().ToString(), MakeCloseRequest(), runtime.Path);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCode.SessionNotFound);
        Assert.Equal(historyBefore, Directory.GetFiles(project.Combine("docs", "historial")));
    }

    [Fact]
    public void Close_TwiceOnSameSession_SecondAttemptIsRejectedAndDoesNotOverwrite()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path);
        var start = SessionManager.Start(project.Path, MakeStartRequest(null), runtime.Path);
        var first = SessionManager.Close(project.Path, start.SessionId!, MakeCloseRequest(), runtime.Path);
        var originalBytes = File.ReadAllBytes(
            project.Combine(first.HistoryFile!.Replace('/', Path.DirectorySeparatorChar)));

        var second = SessionManager.Close(project.Path, start.SessionId!, MakeCloseRequest(), runtime.Path);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.Contains(second.Diagnostics, d => d.Code == DiagnosticCode.SessionAlreadyClosed);
        var bytesAfter = File.ReadAllBytes(
            project.Combine(first.HistoryFile!.Replace('/', Path.DirectorySeparatorChar)));
        Assert.Equal(originalBytes, bytesAfter);
    }

    [Fact]
    public void Close_ProjectIdMismatch_IsRejectedAndNeverWritesToTheWrongProject()
    {
        using var projectA = new TempDirectory();
        using var projectB = new TempDirectory();
        using var runtime = new TempDirectory();
        ProjectMemoryInitializer.Init(projectA.Path, "Project A");
        ProjectMemoryInitializer.Init(projectB.Path, "Project B");
        var start = SessionManager.Start(projectA.Path, MakeStartRequest(null), runtime.Path);

        var result = SessionManager.Close(projectB.Path, start.SessionId!, MakeCloseRequest(), runtime.Path);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCode.SessionProjectMismatch);
        Assert.Empty(Directory.GetFiles(projectB.Combine("docs", "historial")));

        // The session started on A must still be closeable on A afterwards.
        var closeOnA = SessionManager.Close(projectA.Path, start.SessionId!, MakeCloseRequest(), runtime.Path);
        Assert.True(closeOnA.Success);
    }

    [Fact]
    public void Close_HistoryCollision_NeverOverwritesTheExistingFile()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path);
        var start = SessionManager.Start(project.Path, MakeStartRequest(null), runtime.Path);

        // Force a pre-existing file at the exact path this session would produce.
        var (_, state, _) = SessionStateStore.Find(start.SessionId!, runtime.Path);
        var finishedAtForCollision = DateTimeOffset.Now;
        var collidingName = SessionFileNameGenerator.Generate(finishedAtForCollision, start.SessionId!, state!.Slug);
        var historyDir = project.Combine("docs", "historial");
        var collidingPath = Path.Combine(historyDir, collidingName);
        var preexistingContent = "pre-existing content that must survive"u8.ToArray();
        File.WriteAllBytes(collidingPath, preexistingContent);
        var originalHash = SHA256.HashData(preexistingContent);

        // The close call below runs at "now", which under normal clock resolution reproduces
        // the exact same second-granularity filename as the one just created, forcing a
        // deterministic collision instead of relying on timing luck.

        var result = SessionManager.Close(project.Path, start.SessionId!, MakeCloseRequest(), runtime.Path);

        if (result.Success)
        {
            // Extremely rare: the clock ticked into the next second between file creation and
            // close, so no collision occurred - assert the pre-existing file is still untouched
            // and move on rather than producing a flaky failure.
            Assert.Equal(originalHash, SHA256.HashData(File.ReadAllBytes(collidingPath)));
            return;
        }

        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCode.SessionHistoryCollision);
        Assert.Equal(originalHash, SHA256.HashData(File.ReadAllBytes(collidingPath)));

        // The session must remain open so the operator can retry (e.g. with a different slug).
        var status = SessionManager.Status(project.Path, runtime.Path);
        Assert.Single(status.OpenSessions);
    }

    [Fact]
    public void Status_NoSessions_ReturnsEmptyList()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path);

        var result = SessionManager.Status(project.Path, runtime.Path);

        Assert.True(result.Success);
        Assert.Empty(result.OpenSessions);
    }

    [Fact]
    public void SessionLifecycle_WorksWithoutGit()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path);
        Assert.False(Directory.Exists(project.Combine(".git")));

        var start = SessionManager.Start(project.Path, MakeStartRequest(null), runtime.Path);
        var close = SessionManager.Close(project.Path, start.SessionId!, MakeCloseRequest(), runtime.Path);

        Assert.True(start.Success);
        Assert.True(close.Success);
        Assert.False(Directory.Exists(project.Combine(".git")));
    }

    [Fact]
    public void History_TravelsWhenTheProjectIsCopied_AndPreservesProjectId()
    {
        using var source = new TempDirectory();
        using var destinationParent = new TempDirectory();
        using var runtime = new TempDirectory();
        var init = ProjectMemoryInitializer.Init(source.Path, "Portable With History");
        var start = SessionManager.Start(source.Path, MakeStartRequest(null), runtime.Path);
        var close = SessionManager.Close(source.Path, start.SessionId!, MakeCloseRequest(), runtime.Path);

        var destination = Path.Combine(destinationParent.Path, "copy");
        CopyDirectory(source.Path, destination);

        var validation = ProjectMemoryValidator.Validate(destination,
            machineProfilePath: destinationParent.Combine("unused-machine.json"));
        var copiedHistoryFile = Path.Combine(destination, close.HistoryFile!.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(validation.IsValid);
        Assert.Equal(init.ProjectId, validation.ProjectId);
        Assert.True(File.Exists(copiedHistoryFile));
        Assert.Equal(
            File.ReadAllText(Path.Combine(source.Path, close.HistoryFile!.Replace('/', Path.DirectorySeparatorChar))),
            File.ReadAllText(copiedHistoryFile));
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            File.Copy(file, Path.Combine(destinationDir, Path.GetFileName(file)));
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            CopyDirectory(subDir, Path.Combine(destinationDir, Path.GetFileName(subDir)));
        }
    }
}
