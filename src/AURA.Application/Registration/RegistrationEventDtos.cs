using AURA.Domain.Registration;

namespace AURA.Application.Registration;

public sealed record RegisterRegistrationEventRequest(
    Guid EventId,
    int TenantId,
    string ProjectCode,
    string Mode,
    string EventType,
    string Summary,
    string? Details,
    string? ClaimedActorType,
    int? ClaimedActorId,
    string? AgentName,
    string? Host,
    string? Workspace,
    Guid? SessionId,
    string? Model,
    string? Environment,
    string? Source,
    DateTimeOffset? OccurredAtUtc,
    string? ExternalProjectKey,
    string? ProjectName,
    string? IdentityStatus);

public enum RegistrationEventStatus
{
    Created,
    AlreadyExists,
    ValidationFailed,
    Forbidden,
}

public sealed record RegistrationEventResult(
    RegistrationEventStatus Status,
    Guid? EventId,
    long? ItemId,
    string? ProjectResolution,
    IReadOnlyList<string>? ValidationErrors = null);

public sealed record RegistrationEventCommand(
    Guid EventId,
    int TenantId,
    string ProjectCode,
    string Mode,
    RegistrationEventType EventType,
    string Summary,
    string? Details,
    string? ClaimedActorType,
    int? ClaimedActorId,
    string? AgentName,
    string? Host,
    string? Workspace,
    Guid? SessionId,
    string? Model,
    string? Environment,
    string? Source,
    DateTime OccurredAtUtc,
    string? ExternalProjectKey,
    string? ProjectName,
    int CredentialId);

public sealed record RegistrationEventOutcome(
    RegistrationEventStatus Status,
    long? ItemId,
    ProjectRegistrationResolution? ProjectResolution);

public sealed record RegistrationEventDto(
    long ItemId,
    Guid EventId,
    int TenantId,
    int? ProjectId,
    string ProjectCode,
    string Mode,
    string EventType,
    string Summary,
    string? Details,
    string? ClaimedActorType,
    int? ClaimedActorId,
    string? AgentName,
    string? Host,
    string? Workspace,
    Guid? SessionId,
    string? Model,
    string? Environment,
    string? Source,
    string? ExternalProjectKey,
    string? ProjectName,
    DateTime OccurredAtUtc,
    DateTime RegisteredAtUtc,
    string IdentityStatus,
    string ProjectResolution,
    int CredentialId);
