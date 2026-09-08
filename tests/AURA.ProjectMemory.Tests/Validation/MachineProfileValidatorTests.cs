using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Initialization;
using AURA.ProjectMemory.Tests.TestSupport;
using AURA.ProjectMemory.Validation;

namespace AURA.ProjectMemory.Tests.Validation;

public class MachineProfileValidatorTests
{
    [Fact]
    public void Validate_AbsentProfile_IsInfoAndReportsAbsence()
    {
        using var dir = new TempDirectory();
        var path = dir.Combine("machine.json");

        var result = MachineProfileValidator.Validate(path);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, d =>
            d.Code == DiagnosticCode.MachineProfileAbsent && d.Severity == DiagnosticSeverity.Info);
    }

    [Fact]
    public void Validate_ValidProfile_ReturnsMachineIdAndName()
    {
        using var dir = new TempDirectory();
        var path = dir.Combine("machine.json");
        MachineProfileInitializer.Init(path);

        var result = MachineProfileValidator.Validate(path);

        Assert.True(result.IsValid);
        Assert.NotNull(result.MachineId);
        Assert.NotNull(result.MachineName);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Validate_InvalidMachineId_IsWarning()
    {
        using var dir = new TempDirectory();
        var path = dir.Combine("machine.json");
        File.WriteAllText(path, """{ "schemaVersion": "1.0", "machineId": "not-a-uuid", "machineName": "X" }""");

        var result = MachineProfileValidator.Validate(path);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, d =>
            d.Code == DiagnosticCode.MachineProfileInvalid && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Validate_MissingMachineName_IsWarning()
    {
        using var dir = new TempDirectory();
        var path = dir.Combine("machine.json");
        File.WriteAllText(path, "{ \"schemaVersion\": \"1.0\", \"machineId\": \"" + Guid.NewGuid() + "\" }");

        var result = MachineProfileValidator.Validate(path);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, d => d.Code == DiagnosticCode.MachineProfileInvalid);
    }
}
