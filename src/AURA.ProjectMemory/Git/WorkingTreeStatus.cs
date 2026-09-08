namespace AURA.ProjectMemory.Git;

/// <summary>
/// Simple working tree contract (AURA_PROJECT_MEMORY v1.0 Etapa 6 §19): only clean/dirty are
/// distinguished today; per-file status (modified/untracked/staged/...) is deliberately not
/// captured to keep the contract small and to avoid recording more detail than is needed.
/// </summary>
public enum WorkingTreeStatus
{
    Unknown,
    Clean,
    Dirty
}
