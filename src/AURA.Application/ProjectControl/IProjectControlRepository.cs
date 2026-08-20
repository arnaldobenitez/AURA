using AURA.Domain.ProjectControl;

namespace AURA.Application.ProjectControl;

public enum ProjectEventOutcomeStatus
{
    Created,
    AlreadyExists,
    ProjectNotFound,
    Conflict,
    InvalidTransition,
}

public sealed record ProjectEventOutcome(
    ProjectEventOutcomeStatus Status,
    long? ItemId,
    long? CurrentLastItemId,
    string? ErrorMessage = null);

public sealed record ProjectEventCommand(
    int TenantId,
    string ProjectCode,
    Guid EventId,
    long? ExpectedPreviousItemId,
    ProjectEventType EventType,
    ProjectActorType ActorType,
    int? ActorId,
    string? Agent,
    string? Environment,
    ProjectWorkStatus Status,
    string Summary,
    string? Details,
    string? Stage,
    decimal? StageProgress,
    int? TotalStages,
    ProjectActorType? NextControlType,
    int? NextControlId,
    string? NextAction,
    string? Blocker,
    string? Reference,
    string? Model);

public enum CreateProjectOutcomeStatus
{
    Created,
    AlreadyExists,
    Conflict,
}

public sealed record CreateProjectOutcome(CreateProjectOutcomeStatus Status, int ProjectId);

public sealed record CreateProjectCommand(
    int TenantId,
    string ProjectCode,
    string ProjectName,
    string? Description,
    string? WorkingPath,
    string? Repository,
    ProjectStatus Status,
    int? HumanResponsibleId);

public interface IProjectControlRepository
{
    Task<IReadOnlyList<ProjectControlProjectDto>> GetBoardAsync(int tenantId, CancellationToken cancellationToken);

    Task<ProjectEventOutcome> RegisterEventAsync(ProjectEventCommand command, CancellationToken cancellationToken);

    Task<CreateProjectOutcome> CreateProjectAsync(CreateProjectCommand command, CancellationToken cancellationToken);

    Task<ProjectHistoryDto?> GetHistoryAsync(int tenantId, string projectCode, CancellationToken cancellationToken);
}
