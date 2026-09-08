using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Initialization;
using AURA.ProjectMemory.Tests.TestSupport;
using AURA.ProjectMemory.Validation;

namespace AURA.ProjectMemory.Tests.Integrity;

public class ConflictMarkerTests
{
    [Fact]
    public void Validator_CurrentStateWithConflictMarkers_IsInvalid()
    {
        using var project = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path, "Conflict State");

        var currentStatePath = project.Combine("docs", "01-ESTADO-ACTUAL.md");
        File.WriteAllText(currentStatePath,
            "# Estado\n\n<<<<<<< HEAD\nlocal\n=======\nremote\n>>>>>>> branch");

        var result = ProjectMemoryValidator.Validate(project.Path);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, d =>
            d.Code == DiagnosticCode.CurrentStateConflictMarkers && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Validator_HistoryWithConflictMarkers_IsInvalid()
    {
        using var project = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path, "Conflict History");

        var historyFile = project.Combine("docs", "historial", "2026-09-01T00-00-00Z__some-id.md");
        Directory.CreateDirectory(project.Combine("docs", "historial"));
        File.WriteAllText(historyFile,
            "# Sesión\n\n<<<<<<< HEAD\nlocal\n=======\nremote\n>>>>>>> branch");

        var result = ProjectMemoryValidator.Validate(project.Path);

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, d =>
            d.Code == DiagnosticCode.HistoryConflictMarkers && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Validator_NoConflictMarkers_IsValid()
    {
        using var project = new TempDirectory();
        ProjectMemoryInitializer.Init(project.Path, "Clean");

        var result = ProjectMemoryValidator.Validate(project.Path);

        Assert.True(result.IsValid);
        Assert.DoesNotContain(result.Diagnostics, d =>
            d.Code == DiagnosticCode.CurrentStateConflictMarkers || d.Code == DiagnosticCode.HistoryConflictMarkers);
    }
}
