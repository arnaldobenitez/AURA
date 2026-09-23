using AURA.Application.AgentCredential;
using AURA.Domain.ProjectControl;

namespace AURA.Application.ProjectControl;

public enum ProjectEventOutcomeStatus
{
    Created,
    AlreadyExists,
    ProjectNotFound,
    Conflict,
    InvalidTransition,
    // AURA_PROTOCOL v2.1 lease/concurrency outcomes, extended by v2.2 Roadmap approvals - see docs/aura/AURA_PROTOCOL_v2.2.md.
    LeaseConflict,
    LeaseExpired,
}

public sealed record LeaseSnapshot(
    Guid LeaseId,
    long Version,
    DateTime ExpiresAtUtc,
    ProjectActorType HolderActorType,
    int? HolderActorId,
    // Authenticated credential currently backing this lease - null right after a HANDOFF, until
    // whoever was declared as the next control actually authenticates and acts (see
    // docs/aura/AURA_PROTOCOL_v2.1.md "Lease y concurrencia optimista"). Never sent to clients;
    // internal to how the repository verifies who may act next.
    int? HolderCredentialId = null);

public sealed record ProjectEventOutcome(
    ProjectEventOutcomeStatus Status,
    long? ItemId,
    long? CurrentLastItemId,
    string? ErrorMessage = null,
    LeaseSnapshot? Lease = null);

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
    string? Model,
    string? WorkStream = null,
    Guid? LeaseId = null,
    long? ExpectedLeaseVersion = null,
    int? CredentialId = null,
    // Lease duration for a CLAIM/REQUEST that (re)acquires the lease; the repository applies a
    // configured default when null. Kept on the command so the service layer (which reads
    // configuration) controls it rather than the repository hardcoding a value.
    TimeSpan? LeaseDuration = null,
    string? StageCode = null,
    string? StageOutcome = null);

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

    Task<ProjectRoadmapDto?> GetRoadmapAsync(int tenantId, string projectCode, string? workStream, CancellationToken cancellationToken);

    Task<UpdateProjectRoadmapResult> UpdateRoadmapAsync(string projectCode, UpdateProjectRoadmapRequest request, AuthenticatedActor actor, CancellationToken cancellationToken);

    Task<ProjectHistoryDto?> GetHistoryAsync(int tenantId, string projectCode, CancellationToken cancellationToken);
}
