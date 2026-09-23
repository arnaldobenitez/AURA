namespace AURA.Application.ProjectControl;

public sealed record ProjectRoadmapStageDefinition(
    string StageCode,
    string Description,
    int Order,
    string? AcceptanceCriteria,
    int ValidatorCredentialId);

public sealed record UpdateProjectRoadmapRequest(
    int TenantId,
    string? WorkStream,
    Guid EventId,
    IReadOnlyList<ProjectRoadmapStageDefinition> Stages);

public enum UpdateProjectRoadmapStatus
{
    Updated,
    ProjectNotFound,
    ExecNotFound,
    Forbidden,
    InvalidPlan,
    PlanLocked,
}

public sealed record UpdateProjectRoadmapResult(
    UpdateProjectRoadmapStatus Status,
    IReadOnlyList<string>? Errors = null);
