using AURA.Domain.ProjectControl;

namespace AURA.Application.AgentCredential;

public sealed record CreateAgentCredentialRequest(
    int TenantId,
    string ActorType,
    string AgentName,
    int? LinkedActorId,
    IReadOnlyList<int>? ProjectIds = null);

public enum CreateAgentCredentialStatus
{
    Created,
    ValidationFailed,
}

public sealed record CreateAgentCredentialResult(
    CreateAgentCredentialStatus Status,
    int? CredentialId,
    string? PlainSecret,
    IReadOnlyList<string>? Errors);

public sealed record RevokeAgentCredentialRequest(int TenantId, int CredentialId);

public enum RevokeAgentCredentialStatus
{
    Revoked,
    AlreadyRevoked,
    NotFound,
}

public sealed record RevokeAgentCredentialResult(RevokeAgentCredentialStatus Status);

public sealed record AgentCredentialSummaryDto(
    int CredentialId,
    string ActorType,
    string AgentName,
    int? LinkedActorId,
    DateTime CreatedAtUtc,
    DateTime? RevokedAtUtc,
    DateTime? LastUsedAtUtc,
    int? RotatedFromCredentialId);

public sealed record IssueAccessTokenRequest(int TenantId, int CredentialId, string Secret);

public enum IssueAccessTokenStatus
{
    Issued,
    InvalidCredential,
    Revoked,
}

public sealed record IssueAccessTokenResult(
    IssueAccessTokenStatus Status,
    string? AccessToken,
    DateTime? ExpiresAtUtc);

/// <summary>
/// The identity AURA_PROTOCOL v2.0/v2.1 actually trusts for a write: derived from the caller's
/// validated JWT, never from the request body. See docs/aura/AURA_PROTOCOL_v2.md and
/// AURA-PROTOCOL-V2-01's HUMAN-vs-ANALYST section for why ActorType is enforced strictly while
/// ActorId is only enforced when the credential itself pins one (LinkedActorId) - today that is
/// required for HUMAN credentials and optional for CODER/AI_ANALYST ones.
/// </summary>
public sealed record AuthenticatedActor(
    int TenantId,
    int CredentialId,
    ProjectActorType ActorType,
    int? LinkedActorId,
    string AgentName,
    string? AuthenticationSource = null);
