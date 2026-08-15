using AURA.Domain.ProjectControl;

namespace AURA.Application.ProjectControl;

public enum ProjectEventOutcomeStatus
{
    Created,
    AlreadyExists,
    ProjectNotFound,
    Conflict,
}

public sealed record ProjectEventOutcome(
    ProjectEventOutcomeStatus Status,
    long? ItemId,
    long? CurrentLastItemId);

public sealed record ProjectEventCommand(
    int TenantId,
    string ProjectCode,
    Guid EventId,
    long? ExpectedPreviousItemId,
    ProjectEventType EventType,
    ProjectActorType ActorType,
    int? ActorId,
    ProjectWorkStatus Status,
    string Summary,
    string? Details,
    ProjectActorType? NextControlType,
    int? NextControlId,
    string? NextAction,
    string? Blocker,
    string? Reference,
    string? Model);

public interface IProjectControlRepository
{
    Task<IReadOnlyList<ProjectControlProjectDto>> GetBoardAsync(int tenantId, CancellationToken cancellationToken);

    Task<ProjectEventOutcome> RegisterEventAsync(ProjectEventCommand command, CancellationToken cancellationToken);
}
