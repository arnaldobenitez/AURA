namespace AURA.ProjectMemory.Diagnostics;

/// <summary>
/// Severity of a Project Memory diagnostic, per AURA_PROJECT_MEMORY v1.0 §19/§21:
/// ERROR blocks conformance, WARNING flags a declared-but-missing optional module,
/// INFO is purely informative (e.g. AGENTS.md, Git, or the machine profile being absent).
/// </summary>
public enum DiagnosticSeverity
{
    Info,
    Warning,
    Error
}
