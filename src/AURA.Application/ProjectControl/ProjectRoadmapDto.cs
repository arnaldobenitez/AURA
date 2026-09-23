namespace AURA.Application.ProjectControl;

public sealed record ProjectRoadmapStageDto(
    int StageId,
    string? StageCode,
    int Order,
    string Description,
    string? AcceptanceCriteria,
    decimal? Progress,
    string Status,
    int? ValidatorCredentialId,
    string? ValidatorName,
    string? ApprovalOutcome,
    DateTime? ApprovedAtUtc,
    string? ApproverAgent,
    string? ApproverModel,
    string? ApprovalSummary,
    string? EvidenceReference);

public sealed record ProjectRoadmapDto(
    int ProjectId,
    string ProjectCode,
    string ProjectName,
    int? ExecId,
    string? WorkStream,
    int TotalStages,
    int ClosedStages,
    decimal? ExecProgress,
    IReadOnlyList<ProjectRoadmapStageDto> Stages);
