namespace AURA.ProjectMemory.Session;

/// <summary>
/// Ephemeral, local-only record of an open session (AURA_PROJECT_MEMORY v1.0 §10 opening
/// metadata). This is runtime plumbing, not portable Project Memory: it never lives under
/// PROJECT_ROOT, is never versioned, and is discarded once the session closes successfully.
/// <see cref="ProjectRootHint"/> is a diagnostic convenience only - <see cref="ProjectId"/> is
/// the only identity that matters (AURA_PROJECT_MEMORY v1.0 §30).
/// </summary>
public sealed record SessionState(
    string SessionId,
    string ProjectId,
    string StartedAt,
    string AuraRole,
    string Objective,
    string? ProjectRootHint,
    string? LogicalUser,
    string? Agent,
    string? Model,
    string? Slug,
    string? MachineId,
    string? MachineName,
    string? GitBranch = null,
    string? InitialCommit = null,
    string? WorkingTreeInitial = null);
