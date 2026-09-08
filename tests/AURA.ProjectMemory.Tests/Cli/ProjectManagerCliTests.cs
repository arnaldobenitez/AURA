using System.Text.Json;
using AURA.ProjectMemory.Tests.TestSupport;

namespace AURA.ProjectMemory.Tests.Cli;

public class ProjectManagerCliTests
{
    [Fact]
    public void ManagerCommandsProduceParseableJsonAndRoundTripIndex()
    {
        using var project = new TempDirectory();
        using var index = new TempDirectory();
        var indexPath = index.Combine("projects", "index.json");
        var init = CliProcess.Run("project", "init", project.Path, "--name", "Manager CLI", "--json");
        Assert.Equal(0, init.ExitCode);

        var inspect = CliProcess.Run("manager", "inspect", project.Path, "--json");
        Assert.Equal(0, inspect.ExitCode);
        Assert.True(JsonDocument.Parse(inspect.StdOut).RootElement.GetProperty("valid").GetBoolean());

        var remember = CliProcess.Run("manager", "remember", project.Path, "--alias", "aura", "--index-path", indexPath, "--json");
        Assert.Equal(0, remember.ExitCode);
        Assert.True(JsonDocument.Parse(remember.StdOut).RootElement.GetProperty("success").GetBoolean());

        var list = CliProcess.Run("manager", "list-known", "--index-path", indexPath, "--json");
        Assert.Equal(0, list.ExitCode);
        Assert.Equal(1, JsonDocument.Parse(list.StdOut).RootElement.GetArrayLength());

        var history = CliProcess.Run("manager", "history", project.Path, "--take", "5", "--json");
        Assert.Equal(0, history.ExitCode);
        Assert.Equal(JsonValueKind.Array, JsonDocument.Parse(history.StdOut).RootElement.ValueKind);

        var handoff = CliProcess.Run("manager", "handoff", project.Path, "--json");
        Assert.Equal(0, handoff.ExitCode);
        Assert.Equal("Manager CLI", JsonDocument.Parse(handoff.StdOut).RootElement.GetProperty("projectName").GetString());

        var forget = CliProcess.Run("manager", "forget", "aura", "--index-path", indexPath, "--json");
        Assert.Equal(0, forget.ExitCode);
        Assert.True(JsonDocument.Parse(forget.StdOut).RootElement.GetProperty("success").GetBoolean());
    }
}
