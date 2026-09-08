namespace AURA.ProjectMemory.Diagnostics;

/// <summary>
/// A single validation finding. <see cref="Path"/> is relative to PROJECT_ROOT when it refers to
/// project content, or an absolute path only for out-of-project locations such as the machine
/// profile (which intentionally lives outside PROJECT_ROOT per AURA_PROJECT_MEMORY v1.0 §5.2).
/// </summary>
public sealed record Diagnostic(
    DiagnosticSeverity Severity,
    string Code,
    string Message,
    string? Path = null,
    string? SuggestedAction = null);
