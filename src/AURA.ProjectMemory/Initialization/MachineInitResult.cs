using AURA.ProjectMemory.Diagnostics;

namespace AURA.ProjectMemory.Initialization;

/// <summary>
/// Reusable, UI-agnostic outcome of initializing the local machine profile. Introduced in
/// Etapa 6 to replace the Etapa 5 shortcut of overloading <see cref="ProjectInitResult.ProjectId"/>
/// to carry a <c>machineId</c> - a different domain deserves its own result type.
/// </summary>
public sealed record MachineInitResult(
    bool Success,
    string? MachineId,
    string? MachineName,
    IReadOnlyList<Diagnostic> Diagnostics);
