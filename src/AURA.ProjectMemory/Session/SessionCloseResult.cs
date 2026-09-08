using AURA.ProjectMemory.Diagnostics;

namespace AURA.ProjectMemory.Session;

/// <summary>Reusable, UI-agnostic outcome of closing a session.</summary>
public sealed record SessionCloseResult(
    bool Success,
    string? SessionId,
    string? ProjectId,
    string? HistoryFile,
    string? FinishedAt,
    IReadOnlyList<Diagnostic> Diagnostics);
