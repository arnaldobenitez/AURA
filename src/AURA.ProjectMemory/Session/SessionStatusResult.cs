using AURA.ProjectMemory.Diagnostics;

namespace AURA.ProjectMemory.Session;

/// <summary>Reusable, UI-agnostic snapshot of the open sessions of a project.</summary>
public sealed record SessionStatusResult(
    bool Success,
    string? ProjectRoot,
    string? ProjectId,
    IReadOnlyList<SessionState> OpenSessions,
    IReadOnlyList<Diagnostic> Diagnostics);
