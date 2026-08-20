namespace AURA.Application.ProjectControl;

public sealed record ProjectInterventionDto(
    long ItemId,
    DateTime? TimestampUtc,
    string? EventType,
    string? ActorType,
    string? ActorName,
    string? Agent,
    string? Model,
    string? Environment,
    string? WorkStatus,
    string? ExecDescription,
    int? ExecId,
    int? StageId,
    string? Stage,
    decimal? StageProgress,
    decimal? ExecProgress,
    string? NextControlType,
    string? NextControlName,
    string? Summary,
    string? NextAction,
    string? Blocker,
    string? Reference);

public sealed record ProjectHistoryDto(
    int ProjectId,
    string? ProjectCode,
    string ProjectName,
    IReadOnlyList<ProjectInterventionDto> Interventions);
