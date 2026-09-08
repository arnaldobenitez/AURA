using System.Text.Json;
using AURA.ProjectMemory.Tests.TestSupport;

namespace AURA.ProjectMemory.Tests.Cli;

public class AgentBootstrapCliTests
{
    [Fact]
    public void Bootstrap_ValidProject_ExitCodeZero()
    {
        using var project = new TempDirectory();
        CliProcess.Run("project", "init", project.Path, "--name", "AgentBootstrap");

        var (exitCode, stdOut, _) = CliProcess.Run("agent", "bootstrap", project.Path, "--json");

        Assert.Equal(0, exitCode);
        var json = JsonDocument.Parse(stdOut);
        Assert.True(json.RootElement.GetProperty("project").GetProperty("valid").GetBoolean());
    }

    [Fact]
    public void Bootstrap_InvalidProject_ExitCodeOne()
    {
        using var project = new TempDirectory();

        var (exitCode, _, _) = CliProcess.Run("agent", "bootstrap", project.Path, "--json");

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Bootstrap_UnknownOption_ExitCodeTwo()
    {
        using var project = new TempDirectory();

        var (exitCode, _, _) = CliProcess.Run("agent", "bootstrap", project.Path, "--unknown");

        Assert.Equal(2, exitCode);
    }

    [Fact]
    public void Bootstrap_JsonIncludesContractAndCommands()
    {
        using var project = new TempDirectory();
        CliProcess.Run("project", "init", project.Path, "--name", "AgentBootstrap Json");

        var (exitCode, stdOut, _) = CliProcess.Run("agent", "bootstrap", project.Path, "--json");

        Assert.Equal(0, exitCode);
        var json = JsonDocument.Parse(stdOut);
        Assert.True(json.RootElement.TryGetProperty("agentContract", out _));
        Assert.True(json.RootElement.TryGetProperty("commands", out _));
        Assert.True(json.RootElement.TryGetProperty("documentation", out _));
    }
}
