using AURA.ProjectMemory.Agent;
using AURA.ProjectMemory.Initialization;
using AURA.ProjectMemory.Session;
using AURA.ProjectMemory.Tests.TestSupport;

namespace AURA.ProjectMemory.Tests.Agent;

public class AgentBootstrapServiceTests
{
    [Fact]
    public void Bootstrap_ValidProject_ReturnsExpectedStructure()
    {
        using var project = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path, "Bootstrap Test");

        var result = AgentBootstrapService.Bootstrap(project.Path);

        Assert.True(result.Project.Valid);
        Assert.Equal(project.Path, result.Project.ProjectRoot);
        Assert.NotNull(result.Project.ProjectId);
        Assert.Equal("1.0", result.ProjectMemory.Version);
        Assert.Contains(result.ProjectMemory.CoreDocuments, d => d == "AURA.md");
        Assert.Contains(result.ProjectMemory.CoreDocuments, d => d == "docs/01-ESTADO-ACTUAL.md");
        Assert.True(result.AgentContract.SessionRequiredForRelevantWork);
        Assert.True(result.AgentContract.HistoryAppendOnly);
        Assert.NotEmpty(result.Commands.Start);
        Assert.NotEmpty(result.Commands.Close);
    }

    [Fact]
    public void Bootstrap_InvalidProject_ProjectValidIsFalse()
    {
        using var project = new TempDirectory();

        var result = AgentBootstrapService.Bootstrap(project.Path);

        Assert.False(result.Project.Valid);
        Assert.Null(result.Project.ProjectId);
    }

    [Fact]
    public void Bootstrap_WithMachineProfile_IncludesMachineInfo()
    {
        using var project = new TempDirectory();
        using var machine = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path, "With Machine");
        MachineProfileInitializer.Init(machine.Combine("machine.json"));

        var result = AgentBootstrapService.Bootstrap(project.Path, machine.Combine("machine.json"));

        Assert.NotNull(result.Machine);
        Assert.True(result.Machine.Present);
        Assert.NotNull(result.Machine.MachineId);
        Assert.NotNull(result.Machine.MachineName);
    }

    [Fact]
    public void Bootstrap_WithoutMachineProfile_MachineIsNotPresent()
    {
        using var project = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path, "Without Machine");

        var result = AgentBootstrapService.Bootstrap(project.Path, "nonexistent-machine.json");

        Assert.NotNull(result.Machine);
        Assert.False(result.Machine.Present);
    }

    [Fact]
    public void Bootstrap_WithOpenSession_ListsSession()
    {
        using var project = new TempDirectory();
        using var runtime = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path, "With Session");

        var start = SessionManager.Start(project.Path,
            new SessionStartRequest("test bootstrap", "test-agent"), runtime.Path);

        var result = AgentBootstrapService.Bootstrap(project.Path, runtimeRoot: runtime.Path);

        Assert.True(result.Sessions.HasOpenSession);
        Assert.Single(result.Sessions.OpenSessions);
        Assert.Equal(start.SessionId, result.Sessions.OpenSessions[0].SessionId);
    }

    [Fact]
    public void Bootstrap_OptionalModules_ReportsPresence()
    {
        using var project = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path, "Modules");
        File.WriteAllText(project.Combine("docs", "02-DECISIONES.md"), "# Decisiones");

        var result = AgentBootstrapService.Bootstrap(project.Path);

        var decisions = result.ProjectMemory.Modules.FirstOrDefault(m => m.Name == "02-DECISIONES");
        Assert.NotNull(decisions);
        Assert.True(decisions.Present);
    }

    [Fact]
    public void Bootstrap_ProjectWithoutAdr_NoAdrModule()
    {
        using var project = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path, "No ADR");

        var result = AgentBootstrapService.Bootstrap(project.Path);

        Assert.DoesNotContain(result.ProjectMemory.Modules, m => m.Name == "ADR");
        Assert.DoesNotContain(result.ProjectMemory.Modules, m => m.Path != null && m.Path.Contains("ADR.md"));
    }

    [Fact]
    public void Bootstrap_Gitless_Works()
    {
        using var project = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path, "Gitless");

        var result = AgentBootstrapService.Bootstrap(project.Path);

        // Git executable may be present; the project itself is not inside a Git repository.
        Assert.False(result.Git.Repository);
        Assert.Null(result.Git.Branch);
        Assert.Null(result.Git.Commit);
    }
}
