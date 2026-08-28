namespace AURA.Application.ProjectControl;

public enum RegisterProjectEventStatus
{
    Created,
    AlreadyExists,
    ProjectNotFound,
    Conflict,
    ValidationFailed,
    // AURA_PROTOCOL v2.1 - see docs/aura/AURA_PROTOCOL_v2.1.md.
    LeaseConflict,
    Forbidden,
}

public sealed record LeaseInfoDto(
    Guid LeaseId,
    long Version,
    DateTime ExpiresAtUtc,
    string HolderActorType,
    int? HolderActorId);

public sealed record RegisterProjectEventResult(
    RegisterProjectEventStatus Status,
    long? ItemId,
    long? CurrentLastItemId,
    IReadOnlyList<string>? ValidationErrors,
    LeaseInfoDto? Lease = null);
