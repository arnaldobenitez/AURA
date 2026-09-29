namespace AURA.Application.Admin;

public sealed record AdminProjectDto(
    int TenantId,
    int ProjectId,
    string? ProjectCode,
    string ProjectName,
    string? Description,
    string? ExternalProjectKey,
    string Status,
    bool IsActive,
    DateTime? CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    int? CreatedByUserId,
    int? UpdatedByUserId);

public sealed record AdminProjectCreateRequest(
    int TenantId,
    string ProjectCode,
    string ProjectName,
    string? Description,
    string? ExternalProjectKey);

public sealed record AdminProjectUpdateRequest(
    int TenantId,
    string? ProjectCode,
    string ProjectName,
    string? Description,
    string? ExternalProjectKey);

public sealed record AdminCredentialProjectDto(
    int ProjectId,
    string ProjectCode,
    string ProjectName,
    bool IsActive,
    DateTime AssignedAtUtc,
    DateTime? RevokedAtUtc);

public sealed record AdminAgentCredentialDto(
    int TenantId,
    int CredentialId,
    string AgentName,
    string ActorType,
    string Status,
    string ScopeMode,
    DateTime CreatedAtUtc,
    DateTime? ExpiresAtUtc,
    DateTime? RevokedAtUtc,
    DateTime? LastUsedAtUtc,
    int? RotatedFromCredentialId,
    IReadOnlyList<AdminCredentialProjectDto> Projects);

public sealed record AdminCredentialCreateRequest(
    int TenantId,
    string AgentName,
    string ActorType,
    IReadOnlyList<int> ProjectIds,
    DateTime? ExpiresAtUtc);

public sealed record AdminCredentialProvisionedDto(
    int TenantId,
    int CredentialId,
    string AgentName,
    string ActorType,
    string Secret,
    DateTime CreatedAtUtc,
    DateTime? ExpiresAtUtc,
    IReadOnlyList<AdminCredentialProjectDto> Projects);

public enum AdminWriteStatus
{
    Succeeded,
    NotFound,
    Conflict,
    ValidationFailed,
    ProjectScopeRequired,
}

public sealed record AdminWriteResult(AdminWriteStatus Status, int? Id = null, IReadOnlyList<string>? Errors = null);

public sealed record AdminCredentialCreateResult(
    AdminWriteStatus Status,
    AdminCredentialProvisionedDto? Credential = null,
    IReadOnlyList<string>? Errors = null);
