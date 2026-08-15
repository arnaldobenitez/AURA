namespace AURA.Application.ProjectControl;

public sealed record ProjectControlBoardDto(
    int TenantId,
    DateTime GeneratedAtUtc,
    IReadOnlyList<ProjectControlProjectDto> Projects);

public sealed record ProjectControlProjectDto(
    int ProjectId,
    string? ProjectCode,
    string ProjectName,
    string ProjectStatus,
    long? LastItemId,
    DateTime? LastActivityUtc,
    string? LastEventType,
    string? WorkStatus,
    string? LastActorType,
    string? LastActorName,
    string? LastModel,
    string? CurrentControlType,
    string? CurrentControlName,
    string? Summary,
    string? NextAction,
    string? Blocker,
    string? Reference);
