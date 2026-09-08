using AURA.ProjectMemory.Diagnostics;

namespace AURA.ProjectMemory.Validation;

/// <summary>Reusable, UI-agnostic outcome of validating the local machine profile.</summary>
public sealed record MachineValidationResult(
    bool IsValid,
    string ProfilePath,
    string? MachineId,
    string? MachineName,
    IReadOnlyList<Diagnostic> Diagnostics);
