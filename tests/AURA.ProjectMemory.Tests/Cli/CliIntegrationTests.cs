using System.Text.Json;
using AURA.ProjectMemory.Tests.TestSupport;

namespace AURA.ProjectMemory.Tests.Cli;

/// <summary>
/// Launches the real, compiled <c>aura</c> executable end-to-end (AURA_PROJECT_MEMORY v1.0
/// Etapa 5 §49/§50): init, machine init, session start, session status, session close,
/// validate - exercising both human and JSON output.
/// </summary>
public class CliIntegrationTests
{
    [Fact]
    public void FullLifecycle_InitMachineSessionValidate_Succeeds()
    {
        using var project = new TempDirectory();
        using var machineDir = new TempDirectory();
        var machinePath = machineDir.Combine("machine.json");

        var init = CliProcess.Run("project", "init", project.Path, "--name", "CLI E2E", "--json");
        Assert.Equal(0, init.ExitCode);
        var initJson = JsonDocument.Parse(init.StdOut);
        Assert.True(initJson.RootElement.GetProperty("success").GetBoolean());

        var machineInit = CliProcess.Run("machine", "init", "--path", machinePath, "--json");
        Assert.Equal(0, machineInit.ExitCode);
        var machineJson = JsonDocument.Parse(machineInit.StdOut);
        Assert.True(machineJson.RootElement.GetProperty("success").GetBoolean());

        var sessionStart = CliProcess.Run("project", "session", "start",
            "--path", project.Path, "--objective", "CLI end to end", "--role", "coding-agent",
            "--json");
        Assert.Equal(0, sessionStart.ExitCode);
        var startJson = JsonDocument.Parse(sessionStart.StdOut);
        Assert.True(startJson.RootElement.GetProperty("success").GetBoolean());
        var sessionId = startJson.RootElement.GetProperty("sessionId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(sessionId));

        var status = CliProcess.Run("project", "session", "status", "--path", project.Path, "--json");
        Assert.Equal(0, status.ExitCode);
        var statusJson = JsonDocument.Parse(status.StdOut);
        Assert.Equal(1, statusJson.RootElement.GetProperty("openSessions").GetArrayLength());

        var payloadPath = project.Combine("close-payload.json");
        File.WriteAllText(payloadPath, """
            { "result": "Done via CLI", "pending": "Nothing", "nextRecommendedStep": "Ship it" }
            """);

        var close = CliProcess.Run("project", "session", "close",
            "--path", project.Path, "--session", sessionId!, "--input", payloadPath, "--json");
        Assert.Equal(0, close.ExitCode);
        var closeJson = JsonDocument.Parse(close.StdOut);
        Assert.True(closeJson.RootElement.GetProperty("success").GetBoolean());
        var historyFile = closeJson.RootElement.GetProperty("historyFile").GetString();
        Assert.StartsWith("docs/historial/", historyFile);
        Assert.True(File.Exists(project.Combine(historyFile!.Replace('/', Path.DirectorySeparatorChar))));

        var validate = CliProcess.Run("project", "validate", project.Path, "--json");
        Assert.Equal(0, validate.ExitCode);
        var validateJson = JsonDocument.Parse(validate.StdOut);
        Assert.True(validateJson.RootElement.GetProperty("valid").GetBoolean());
    }

    [Fact]
    public void Init_Twice_ProducesHumanReadableFailureAndExitCodeOne()
    {
        using var project = new TempDirectory();
        CliProcess.Run("project", "init", project.Path);

        var second = CliProcess.Run("project", "init", project.Path);

        Assert.Equal(1, second.ExitCode);
        Assert.Contains("FAILED", second.StdOut);
    }

    [Fact]
    public void SessionClose_UnknownSession_ReturnsExitCodeOne()
    {
        using var project = new TempDirectory();
        CliProcess.Run("project", "init", project.Path);
        var payloadPath = project.Combine("close-payload.json");
        File.WriteAllText(payloadPath, """{ "result": "x", "pending": "y", "nextRecommendedStep": "z" }""");

        var close = CliProcess.Run("project", "session", "close",
            "--path", project.Path, "--session", Guid.NewGuid().ToString(), "--input", payloadPath, "--json");

        Assert.Equal(1, close.ExitCode);
        var json = JsonDocument.Parse(close.StdOut);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public void MachineValidate_AbsentProfile_ReturnsExitCodeOneAndInfoDiagnostic()
    {
        using var machineDir = new TempDirectory();
        var machinePath = machineDir.Combine("absent.json");

        var result = CliProcess.Run("machine", "validate", "--path", machinePath, "--json");

        Assert.Equal(1, result.ExitCode);
        var json = JsonDocument.Parse(result.StdOut);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
    }

    [Fact]
    public void RootHelp_ExitsZeroAndListsCommands()
    {
        var result = CliProcess.Run("--help");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("aura project", result.StdOut);
        Assert.Contains("aura machine", result.StdOut);
    }
}
