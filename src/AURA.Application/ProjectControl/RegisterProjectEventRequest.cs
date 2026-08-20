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
    string? Model);
