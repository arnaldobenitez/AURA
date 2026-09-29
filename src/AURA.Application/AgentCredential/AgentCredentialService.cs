using System.Security.Cryptography;
using AURA.Application.Security;
using AURA.Domain.ProjectControl;

namespace AURA.Application.AgentCredential;

public sealed class AgentCredentialService(IAgentCredentialRepository repository, IAccessTokenIssuer tokenIssuer)
{
    private const int SecretByteLength = 32;
    private const int AgentNameMaxLength = 100;

    public async Task<CreateAgentCredentialResult> CreateAsync(CreateAgentCredentialRequest request, CancellationToken cancellationToken, int? createdByUserId = null)
    {
        var errors = new List<string>();

        if (request.TenantId <= 0)
        {
            errors.Add("tenantId debe ser mayor a cero.");
        }

        if (!TryParseActorType(request.ActorType, out var actorType) || actorType == ProjectActorType.Unassigned)
        {
            errors.Add("actorType debe ser HUMAN, AI_ANALYST, CODER o INGESTION.");
        }
        else if (actorType == ProjectActorType.Ingestion)
        {
            errors.Add("INGESTION requiere proyectos asociados; utilice el flujo administrativo con scope por proyecto.");
        }

        if (string.IsNullOrWhiteSpace(request.AgentName))
        {
            errors.Add("agentName es obligatorio.");
        }
        else if (request.AgentName.Trim().Length > AgentNameMaxLength)
        {
            errors.Add($"agentName no puede superar {AgentNameMaxLength} caracteres.");
        }

        // linkedActorId is optional for every role: when set, the server rejects any actor.id
        // in the request body that disagrees with it (closes identity spoofing for whichever
        // credentials choose to pin one); when left null, actor.id is accepted as asserted -
        // today none of these ids are a real access-control boundary (see
        // docs/aura/AURA_PROTOCOL_v2.1.md "HUMAN vs ANALYST" for the accepted, documented gap).
        if (request.LinkedActorId is { } linkedId && linkedId <= 0)
        {
            errors.Add("linkedActorId debe ser mayor a cero.");
        }

        if (errors.Count > 0)
        {
            return new CreateAgentCredentialResult(CreateAgentCredentialStatus.ValidationFailed, null, null, errors);
        }

        var secretBytes = RandomNumberGenerator.GetBytes(SecretByteLength);
        var plainSecret = Convert.ToBase64String(secretBytes);
        var (salt, hash) = Pbkdf2PasswordHasher.Hash(secretBytes);

        var credentialId = await repository.CreateAsync(
            new NewAgentCredentialRecord(
                request.TenantId,
                0, // asignado por el repositorio (mismo patron IdX secuencial por tenant que el resto del esquema)
                actorType,
                request.AgentName.Trim(),
                request.LinkedActorId,
                salt,
                hash,
                Pbkdf2PasswordHasher.Iterations,
                DateTime.UtcNow,
                null,
                createdByUserId),
            cancellationToken);

        return new CreateAgentCredentialResult(CreateAgentCredentialStatus.Created, credentialId, plainSecret, null);
    }

    public async Task<CreateAgentCredentialResult> RotateAsync(int tenantId, int credentialId, CancellationToken cancellationToken)
    {
        var existing = await repository.FindForAuthenticationAsync(tenantId, credentialId, cancellationToken);
        if (existing is null)
        {
            return new CreateAgentCredentialResult(CreateAgentCredentialStatus.ValidationFailed, null, null, ["Credencial no encontrada."]);
        }
        if (existing.ActorType == ProjectActorType.Ingestion)
        {
            return new CreateAgentCredentialResult(CreateAgentCredentialStatus.ValidationFailed, null, null, ["Las credenciales INGESTION deben rotarse mediante el flujo administrativo con scope por proyecto."]);
        }

        await repository.RevokeAsync(tenantId, credentialId, cancellationToken);

        var secretBytes = RandomNumberGenerator.GetBytes(SecretByteLength);
        var plainSecret = Convert.ToBase64String(secretBytes);
        var (salt, hash) = Pbkdf2PasswordHasher.Hash(secretBytes);

        var newCredentialId = await repository.CreateAsync(
            new NewAgentCredentialRecord(
                tenantId,
                0,
                existing.ActorType,
                existing.AgentName,
                existing.LinkedActorId,
                salt,
                hash,
                Pbkdf2PasswordHasher.Iterations,
                DateTime.UtcNow,
                credentialId),
            cancellationToken);

        return new CreateAgentCredentialResult(CreateAgentCredentialStatus.Created, newCredentialId, plainSecret, null);
    }

    public Task<RevokeAgentCredentialResult> RevokeAsync(RevokeAgentCredentialRequest request, CancellationToken cancellationToken) =>
        RevokeCoreAsync(request.TenantId, request.CredentialId, cancellationToken);

    private async Task<RevokeAgentCredentialResult> RevokeCoreAsync(int tenantId, int credentialId, CancellationToken cancellationToken)
    {
        var status = await repository.RevokeAsync(tenantId, credentialId, cancellationToken);
        return new RevokeAgentCredentialResult(status);
    }

    public Task<IReadOnlyList<AgentCredentialSummaryDto>> ListAsync(int tenantId, CancellationToken cancellationToken) =>
        repository.ListAsync(tenantId, cancellationToken);

    public async Task<IssueAccessTokenResult> IssueAccessTokenAsync(IssueAccessTokenRequest request, CancellationToken cancellationToken)
    {
        var record = await repository.FindForAuthenticationAsync(request.TenantId, request.CredentialId, cancellationToken);
        if (record is null)
        {
            return new IssueAccessTokenResult(IssueAccessTokenStatus.InvalidCredential, null, null);
        }

        if (record.RevokedAtUtc is not null || record.ExpiresAtUtc is { } credentialExpiry && credentialExpiry <= DateTime.UtcNow)
        {
            return new IssueAccessTokenResult(IssueAccessTokenStatus.Revoked, null, null);
        }

        byte[] suppliedSecretBytes;
        try
        {
            suppliedSecretBytes = Convert.FromBase64String(request.Secret);
        }
        catch (FormatException)
        {
            return new IssueAccessTokenResult(IssueAccessTokenStatus.InvalidCredential, null, null);
        }

        if (!Pbkdf2PasswordHasher.Verify(suppliedSecretBytes, record.SecretSalt, record.SecretHash, record.SecretIterations))
        {
            return new IssueAccessTokenResult(IssueAccessTokenStatus.InvalidCredential, null, null);
        }

        await repository.TouchLastUsedAsync(request.TenantId, request.CredentialId, cancellationToken);

        var (accessToken, expiresAtUtc) = tokenIssuer.Issue(new AuthenticatedActor(
            record.TenantId, record.CredentialId, record.ActorType, record.LinkedActorId, record.AgentName));

        return new IssueAccessTokenResult(IssueAccessTokenStatus.Issued, accessToken, expiresAtUtc);
    }

    private static bool TryParseActorType(string? value, out ProjectActorType result)
    {
        switch (value?.Trim().ToUpperInvariant())
        {
            case "HUMAN": result = ProjectActorType.Human; return true;
            case "AI_ANALYST": result = ProjectActorType.AiAnalyst; return true;
            case "CODER": result = ProjectActorType.Coder; return true;
            case "INGESTION": result = ProjectActorType.Ingestion; return true;
            default: result = ProjectActorType.Unassigned; return false;
        }
    }
}
