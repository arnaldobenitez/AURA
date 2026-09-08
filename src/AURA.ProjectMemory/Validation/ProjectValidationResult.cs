using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Manifest;

namespace AURA.ProjectMemory.Validation;

/// <summary>
/// Reusable, UI-agnostic outcome of validating a Project Memory project. Intended to be consumed
/// directly by future tooling (e.g. an AURA Project Manager) without depending on console output.
/// </summary>
public sealed record ProjectValidationResult(
    bool IsValid,
    string? ProjectRoot,
    string? ProjectId,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public IEnumerable<Diagnostic> Errors => Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);

    public IEnumerable<Diagnostic> Warnings => Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning);

    public IEnumerable<Diagnostic> Infos => Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Info);
}
