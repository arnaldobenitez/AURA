using AURA.ProjectMemory.Diagnostics;

namespace AURA.ProjectMemory.Git;

/// <summary>
/// Reusable, UI-agnostic snapshot of Git context for a path (AURA_PROJECT_MEMORY v1.0 Etapa 6
/// §9/§53). Never carries remote URLs, credentials, or user.name/user.email (§44) - only
/// <see cref="Branch"/> (null when detached or unknown), <see cref="Commit"/>, and
/// <see cref="WorkingTree"/>.
/// </summary>
public sealed record GitContext(
    GitAvailability Availability,
    string? Branch,
    string? Commit,
    WorkingTreeStatus WorkingTree,
    bool HasMergeConflicts,
    IReadOnlyList<Diagnostic> Diagnostics)
{
    public static GitContext Unavailable(IReadOnlyList<Diagnostic>? diagnostics = null) =>
        new(GitAvailability.Unavailable, null, null, WorkingTreeStatus.Unknown, false, diagnostics ?? []);

    public static GitContext NotARepository(IReadOnlyList<Diagnostic>? diagnostics = null) =>
        new(GitAvailability.NotRepository, null, null, WorkingTreeStatus.Unknown, false, diagnostics ?? []);
}
