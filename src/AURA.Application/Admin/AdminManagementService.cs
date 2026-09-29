using System.Security.Cryptography;
using AURA.Application.Security;
using AURA.Application.Registration;

namespace AURA.Application.Admin;

public sealed class AdminManagementService(IAdminManagementRepository repository)
{
    private const int AgentNameMaxLength = 100;

    public Task<IReadOnlyList<AdminProjectDto>> ListProjectsAsync(int tenantId, CancellationToken cancellationToken) =>
        repository.ListProjectsAsync(tenantId, cancellationToken);

    public Task<AdminProjectDto?> GetProjectAsync(int tenantId, int projectId, CancellationToken cancellationToken) =>
        repository.GetProjectAsync(tenantId, projectId, cancellationToken);

    public Task<AdminWriteResult> CreateProjectAsync(AdminProjectCreateRequest request, int? actorUserId, CancellationToken cancellationToken)
    {
        var errors = ValidateProject(request.TenantId, request.ProjectCode, request.ProjectName, request.Description, request.ExternalProjectKey, requireCode: true);
        return errors.Count > 0
            ? Task.FromResult(new AdminWriteResult(AdminWriteStatus.ValidationFailed, Errors: errors))
            : repository.CreateProjectAsync(request with { ProjectCode = request.ProjectCode.Trim().ToUpperInvariant(), ProjectName = request.ProjectName.Trim() }, actorUserId, cancellationToken);
    }

    public Task<AdminWriteResult> UpdateProjectAsync(int tenantId, int projectId, AdminProjectUpdateRequest request, int? actorUserId, CancellationToken cancellationToken)
    {
        var errors = ValidateProject(tenantId, request.ProjectCode, request.ProjectName, request.Description, request.ExternalProjectKey, requireCode: false);
        return errors.Count > 0
            ? Task.FromResult(new AdminWriteResult(AdminWriteStatus.ValidationFailed, Errors: errors))
            : repository.UpdateProjectAsync(tenantId, projectId, request with
            {
                ProjectCode = string.IsNullOrWhiteSpace(request.ProjectCode) ? null : request.ProjectCode.Trim().ToUpperInvariant(),
                ProjectName = request.ProjectName.Trim(),
            }, actorUserId, cancellationToken);
    }

    public Task<AdminWriteResult> SetProjectStatusAsync(int tenantId, int projectId, string status, int? actorUserId, CancellationToken cancellationToken)
    {
        if (tenantId <= 0 || !IsProjectStatus(status))
        {
            return Task.FromResult(new AdminWriteResult(AdminWriteStatus.ValidationFailed, Errors: ["Tenant o estado de proyecto inválido."]));
        }

        return repository.SetProjectStatusAsync(tenantId, projectId, status.ToUpperInvariant(), actorUserId, cancellationToken);
    }

    public async Task<AdminCredentialCreateResult> CreateCredentialAsync(AdminCredentialCreateRequest request, int? actorUserId, CancellationToken cancellationToken)
    {
        var errors = ValidateCredential(request);
        if (errors.Count > 0)
        {
            return new AdminCredentialCreateResult(AdminWriteStatus.ValidationFailed, Errors: errors);
        }

        var secretBytes = RandomNumberGenerator.GetBytes(32);
        var secret = Convert.ToBase64String(secretBytes);
        var (salt, hash) = Pbkdf2PasswordHasher.Hash(secretBytes);
        var createdAtUtc = DateTime.UtcNow;
        var normalizedProjectIds = request.ProjectIds.Distinct().Order().ToArray();
        var record = new ScopedCredentialRecord(
            request.TenantId,
            0,
            request.AgentName.Trim(),
            "INGESTION",
            salt,
            hash,
            Pbkdf2PasswordHasher.Iterations,
            createdAtUtc,
            request.ExpiresAtUtc?.ToUniversalTime(),
            actorUserId,
            null);

        var result = await repository.CreateCredentialAsync(record, normalizedProjectIds, actorUserId, cancellationToken);
        if (result.Status != AdminWriteStatus.Succeeded || result.Credential is null)
        {
            return result;
        }

        return result with { Credential = result.Credential with { Secret = secret } };
    }

    public async Task<AdminCredentialCreateResult> RotateCredentialAsync(int tenantId, int credentialId, int? actorUserId, CancellationToken cancellationToken)
    {
        if (tenantId <= 0 || credentialId <= 0)
        {
            return new AdminCredentialCreateResult(AdminWriteStatus.ValidationFailed, Errors: ["tenantId y credentialId deben ser mayores a cero."]);
        }

        var secretBytes = RandomNumberGenerator.GetBytes(32);
        var secret = Convert.ToBase64String(secretBytes);
        var (salt, hash) = Pbkdf2PasswordHasher.Hash(secretBytes);
        var record = new ScopedCredentialRecord(
            tenantId,
            0,
            string.Empty,
            string.Empty,
            salt,
            hash,
            Pbkdf2PasswordHasher.Iterations,
            DateTime.UtcNow,
            null,
            actorUserId,
            credentialId);
        var result = await repository.RotateCredentialAsync(tenantId, credentialId, record, actorUserId, cancellationToken);
        if (result.Status != AdminWriteStatus.Succeeded || result.Credential is null)
        {
            return result;
        }

        return result with { Credential = result.Credential with { Secret = secret } };
    }

    public Task<AdminWriteResult> RevokeCredentialAsync(int tenantId, int credentialId, int? actorUserId, CancellationToken cancellationToken) =>
        repository.RevokeCredentialAsync(tenantId, credentialId, actorUserId, cancellationToken);

    public Task<AdminWriteResult> AssignProjectAsync(int tenantId, int credentialId, int projectId, int? actorUserId, CancellationToken cancellationToken) =>
        repository.AssignProjectAsync(tenantId, credentialId, projectId, actorUserId, cancellationToken);

    public Task<AdminWriteResult> RevokeProjectAssignmentAsync(int tenantId, int credentialId, int projectId, int? actorUserId, CancellationToken cancellationToken) =>
        repository.RevokeProjectAssignmentAsync(tenantId, credentialId, projectId, actorUserId, cancellationToken);

    public Task<IReadOnlyList<AdminAgentCredentialDto>> ListCredentialsAsync(int tenantId, CancellationToken cancellationToken) =>
        repository.ListCredentialsAsync(tenantId, cancellationToken);

    public Task<IReadOnlyList<AdminCredentialProjectDto>> ListCredentialProjectsAsync(int tenantId, int credentialId, CancellationToken cancellationToken) =>
        repository.ListCredentialProjectsAsync(tenantId, credentialId, cancellationToken);

    private static List<string> ValidateCredential(AdminCredentialCreateRequest request)
    {
        var errors = new List<string>();
        if (request.TenantId <= 0)
        {
            errors.Add("tenantId debe ser mayor a cero.");
        }
        if (!string.Equals(request.ActorType?.Trim(), "INGESTION", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("actorType debe ser INGESTION para credenciales técnicas de registro.");
        }
        if (string.IsNullOrWhiteSpace(request.AgentName) || request.AgentName.Trim().Length > AgentNameMaxLength)
        {
            errors.Add($"agentName es obligatorio y no puede superar {AgentNameMaxLength} caracteres.");
        }
        else if (SensitiveContentValidator.ContainsSecret(request.AgentName))
        {
            errors.Add("agentName contiene material sensible y no puede persistirse.");
        }
        if (request.ProjectIds is null || request.ProjectIds.Count == 0 || request.ProjectIds.Any(id => id <= 0))
        {
            errors.Add("Debe asociarse al menos un proyecto válido.");
        }
        else if (request.ProjectIds.Distinct().Count() != request.ProjectIds.Count)
        {
            errors.Add("No se puede repetir un proyecto en la misma solicitud.");
        }
        else if (request.ProjectIds.Count > 1000)
        {
            errors.Add("Una credencial no puede asociarse a más de 1000 proyectos en una operación.");
        }
        if (request.ExpiresAtUtc is { } expiry && expiry <= DateTime.UtcNow)
        {
            errors.Add("expiresAtUtc debe ser una fecha futura.");
        }
        return errors;
    }

    private static List<string> ValidateProject(int tenantId, string? code, string? name, string? description, string? externalKey, bool requireCode)
    {
        var errors = new List<string>();
        if (tenantId <= 0) errors.Add("tenantId debe ser mayor a cero.");
        if (requireCode && string.IsNullOrWhiteSpace(code)) errors.Add("projectCode es obligatorio.");
        if (code is { } projectCode && projectCode.Trim().Length > 50) errors.Add("projectCode no puede superar 50 caracteres.");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100) errors.Add("projectName es obligatorio y no puede superar 100 caracteres.");
        if (description is { Length: > 500 }) errors.Add("description no puede superar 500 caracteres.");
        if (externalKey is { Length: > 200 }) errors.Add("externalProjectKey no puede superar 200 caracteres.");
        if (SensitiveContentValidator.ContainsSecret(code) || SensitiveContentValidator.ContainsSecret(name) ||
            SensitiveContentValidator.ContainsSecret(description) || SensitiveContentValidator.ContainsSecret(externalKey))
        {
            errors.Add("Los campos del proyecto no pueden contener contraseñas, tokens, claves ni otro material sensible.");
        }
        return errors;
    }

    private static bool IsProjectStatus(string? status) => status?.Trim().ToUpperInvariant() is "ACTIVE" or "PAUSED" or "ARCHIVED";
}
