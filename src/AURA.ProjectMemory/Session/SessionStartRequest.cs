namespace AURA.ProjectMemory.Session;

/// <summary>
/// Caller-supplied information for starting a session. Nothing here is inferred automatically:
/// the executor (human or agent) provides it explicitly, per AURA_PROJECT_MEMORY v1.0 §7/§9.1.
/// </summary>
public sealed record SessionStartRequest(
    string Objective,
    string AuraRole,
    string? LogicalUser = null,
    string? Agent = null,
    string? Model = null,
    string? Slug = null,
    string? MachineProfilePath = null);
