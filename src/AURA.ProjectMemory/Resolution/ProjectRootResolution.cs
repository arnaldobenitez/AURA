using AURA.ProjectMemory.Diagnostics;
using AURA.ProjectMemory.Manifest;

namespace AURA.ProjectMemory.Resolution;

/// <summary>Result of walking up from a start path to find PROJECT_ROOT.</summary>
public sealed record ProjectRootResolution(
    bool Found,
    string? ProjectRoot,
    ProjectManifest? Manifest,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public static ProjectRootResolution NotFound(IReadOnlyList<Diagnostic>? diagnostics = null) =>
        new(false, null, null, diagnostics ?? []);
}
