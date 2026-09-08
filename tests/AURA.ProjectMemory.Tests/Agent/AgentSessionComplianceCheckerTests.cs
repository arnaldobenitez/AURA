using AURA.ProjectMemory.Agent;
using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Initialization;
using AURA.ProjectMemory.Session;
using AURA.ProjectMemory.Tests.TestSupport;

namespace AURA.ProjectMemory.Tests.Agent;

public class AgentSessionComplianceCheckerTests
{
    [Fact]
    public void Check_NoActiveSession_IsNotCompliant()
    {
        using var project = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path);

        var result = AgentSessionComplianceChecker.Check(project.Path, Guid.NewGuid().ToString());

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Issues, d => d.Code == DiagnosticCode.SessionNotFound);
    }

    [Fact]
    public void Check_ActiveSession_IsCompliant()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path);
        var start = SessionManager.Start(project.Path,
            new SessionStartRequest("objective", "role"), runtime.Path);

        var result = AgentSessionComplianceChecker.Check(project.Path, start.SessionId!, runtime.Path);

        Assert.True(result.IsCompliant);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Check_AlreadyClosed_IsNotCompliant()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path);
        var start = SessionManager.Start(project.Path,
            new SessionStartRequest("objective", "role"), runtime.Path);
        SessionManager.Close(project.Path, start.SessionId!,
            new SessionCloseRequest("result", "pending", "next"), runtime.Path);

        var result = AgentSessionComplianceChecker.Check(project.Path, start.SessionId!, runtime.Path);

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Issues, d => d.Code == DiagnosticCode.SessionAlreadyClosed);
    }

    [Fact]
    public void Check_CurrentStateWithConflictMarkers_IsNotCompliant()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path);

        var start = SessionManager.Start(project.Path,
            new SessionStartRequest("objective", "role"), runtime.Path);
        Assert.True(start.Success);

        File.WriteAllText(project.Combine("docs", "01-ESTADO-ACTUAL.md"),
            "# Estado\n\n<<<<<<< HEAD\nlocal\n=======\nremote\n>>>>>>> branch");

        var result = AgentSessionComplianceChecker.Check(project.Path, start.SessionId!, runtime.Path);

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Issues, d => d.Code == DiagnosticCode.CurrentStateConflictMarkers);
    }

    [Fact]
    public void Check_ProjectInvalid_IsNotCompliant()
    {
        using var project = new TempDirectory();

        var result = AgentSessionComplianceChecker.Check(project.Path, Guid.NewGuid().ToString());

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Issues, d => d.Severity == DiagnosticSeverity.Error);
    }
}
