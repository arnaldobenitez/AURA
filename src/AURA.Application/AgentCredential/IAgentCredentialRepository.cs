using AURA.Domain.ProjectControl;

namespace AURA.Application.AgentCredential;

public sealed record NewAgentCredentialRecord(
    int TenantId,
    int CredentialId,
    ProjectActorType ActorType,
    string AgentName,
    int? LinkedActorId,
    byte[] SecretSalt,
    byte[] SecretHash,
    int SecretIterations,
    DateTime CreatedAtUtc,
    int? RotatedFromCredentialId);

public sealed record AgentCredentialAuthRecord(
    int TenantId,
    int CredentialId,
    ProjectActorType ActorType,
    string AgentName,
    int? LinkedActorId,
    byte[] SecretSalt,
    byte[] SecretHash,
    int SecretIterations,
    DateTime? RevokedAtUtc);

public interface IAgentCredentialRepository
{
    Task<int> CreateAsync(NewAgentCredentialRecord record, CancellationToken cancellationToken);

    Task<AgentCredentialAuthRecord?> FindForAuthenticationAsync(int tenantId, int credentialId, CancellationToken cancellationToken);

    Task TouchLastUsedAsync(int tenantId, int credentialId, CancellationToken cancellationToken);

    Task<RevokeAgentCredentialStatus> RevokeAsync(int tenantId, int credentialId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AgentCredentialSummaryDto>> ListAsync(int tenantId, CancellationToken cancellationToken);
}
