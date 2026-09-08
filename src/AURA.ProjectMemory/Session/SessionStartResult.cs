using AURA.ProjectMemory.Diagnostics;

namespace AURA.ProjectMemory.Session;

/// <summary>Reusable, UI-agnostic outcome of starting a session.</summary>
public sealed record SessionStartResult(
    bool Success,
    string? SessionId,
    string? ProjectId,
    string? StartedAt,
    IReadOnlyList<Diagnostic> Diagnostics);
