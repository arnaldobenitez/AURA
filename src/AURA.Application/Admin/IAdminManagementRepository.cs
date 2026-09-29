using AURA.Application.AgentCredential;

namespace AURA.Application.Admin;

public sealed record ScopedCredentialRecord(
    int TenantId,
    int CredentialId,
    string AgentName,
    string ActorType,
    byte[] SecretSalt,
    byte[] SecretHash,
    int SecretIterations,
    DateTime CreatedAtUtc,
    DateTime? ExpiresAtUtc,
    int? CreatedByUserId,
    int? RotatedFromCredentialId);

public interface IAdminManagementRepository
{
    Task<IReadOnlyList<AdminProjectDto>> ListProjectsAsync(int tenantId, CancellationToken cancellationToken);
    Task<AdminProjectDto?> GetProjectAsync(int tenantId, int projectId, CancellationToken cancellationToken);
    Task<AdminWriteResult> CreateProjectAsync(AdminProjectCreateRequest request, int? actorUserId, CancellationToken cancellationToken);
    Task<AdminWriteResult> UpdateProjectAsync(int tenantId, int projectId, AdminProjectUpdateRequest request, int? actorUserId, CancellationToken cancellationToken);
    Task<AdminWriteResult> SetProjectStatusAsync(int tenantId, int projectId, string status, int? actorUserId, CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminAgentCredentialDto>> ListCredentialsAsync(int tenantId, CancellationToken cancellationToken);
    Task<AdminCredentialCreateResult> CreateCredentialAsync(ScopedCredentialRecord credential, IReadOnlyList<int> projectIds, int? actorUserId, CancellationToken cancellationToken);
    Task<AdminCredentialCreateResult> RotateCredentialAsync(int tenantId, int credentialId, ScopedCredentialRecord replacement, int? actorUserId, CancellationToken cancellationToken);
    Task<AdminWriteResult> RevokeCredentialAsync(int tenantId, int credentialId, int? actorUserId, CancellationToken cancellationToken);
    Task<AdminWriteResult> AssignProjectAsync(int tenantId, int credentialId, int projectId, int? actorUserId, CancellationToken cancellationToken);
    Task<AdminWriteResult> RevokeProjectAssignmentAsync(int tenantId, int credentialId, int projectId, int? actorUserId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminCredentialProjectDto>> ListCredentialProjectsAsync(int tenantId, int credentialId, CancellationToken cancellationToken);

}
