using AURA.ProjectMemory.Diagnostics;

namespace AURA.ProjectMemory.Initialization;

/// <summary>Reusable, UI-agnostic outcome of initializing a Project Memory Core.</summary>
public sealed record ProjectInitResult(
    bool Success,
    string? ProjectRoot,
    string? ProjectId,
    IReadOnlyList<Diagnostic> Diagnostics);
