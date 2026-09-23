namespace AURA.Application.ProjectControl;

public sealed record ProjectActorRefDto(string Type, int? Id);

public sealed record RegisterProjectEventRequest(
    int TenantId,
    string ProjectCode,
    Guid EventId,
    long? ExpectedPreviousItemId,
    string EventType,
    ProjectActorRefDto Actor,
    string? Agent,
    string? Environment,
    string Status,
    string Summary,
    string? Details,
    string? Stage,
    decimal? StageProgress,
    int? TotalStages,
    ProjectActorRefDto? NextControl,
    string? NextAction,
    string? Blocker,
    string? Reference,
    string? Model,
    // AURA_PROTOCOL v2.1: independent concurrency lane within the project (null = whole
    // project, today's behavior). See docs/aura/AURA_PROTOCOL_v2.1.md.
    string? WorkStream = null,
    // Required for every mutating event except REQUEST/CLAIM, which establish the lease
    // themselves. See docs/aura/AURA_PROTOCOL_v2.1.md "Lease and optimistic concurrency".
    Guid? LeaseId = null,
    long? ExpectedLeaseVersion = null,
    string? StageCode = null,
    string? StageOutcome = null);
