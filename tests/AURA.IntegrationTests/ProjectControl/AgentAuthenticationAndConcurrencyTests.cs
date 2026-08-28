using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using AURA.Application.AgentCredential;
using AURA.Application.ProjectControl;
using AURA.Infrastructure.AgentCredential;
using Microsoft.IdentityModel.Tokens;

namespace AURA.IntegrationTests.ProjectControl;

/// <summary>
/// Covers the security/concurrency test matrix required by AURA-PROTOCOL-V2-01: invalid/revoked
/// credentials, expired tokens, identity spoofing, tenant isolation, lease conflicts (same
/// project, second CLAIM; expired lease; stale ExpectedLeaseVersion) and two independent
/// workstreams progressing concurrently without blocking each other. See
/// docs/aura/AURA_PROTOCOL_v2.1.md.
/// </summary>
[Collection(ProjectControlDatabaseCollection.Name)]
public sealed class AgentAuthenticationAndConcurrencyTests(ProjectControlDatabaseFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static void RequireDatabase(ProjectControlDatabaseFixture fixture)
    {
        Skip.IfNot(fixture.IsAvailable, "Requiere conectividad con el SQL Server de desarrollo (AURA_DESIGNTIME_CONNSTR o User Secrets de AURA.Api).");
    }

    private async Task<HttpResponseMessage> PostEventRawAsync(
        string accessToken, int tenantId, string projectCode, Guid eventId, long? expectedPreviousItemId, string summary,
        string actorType = "CODER", int actorId = 1, string? model = "Sonnet 5",
        string? agent = "CLAUDE_CODE", string? environment = "VS Code", string eventType = "CLAIM",
        string? workStream = null, Guid? leaseId = null, long? expectedLeaseVersion = null)
    {
        var request = new RegisterProjectEventRequest(
            tenantId, projectCode, eventId, expectedPreviousItemId, eventType,
            new ProjectActorRefDto(actorType, actorId), agent, environment,
            "IN_PROGRESS", summary, null, null, null, null, null, null, null, null, model,
            workStream, leaseId, expectedLeaseVersion);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/project-control/events")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        fixture.AddBearer(httpRequest, accessToken);
        return await fixture.Client.SendAsync(httpRequest);
    }

    private async Task<(long ItemId, Guid? LeaseId, long? LeaseVersion)> PostEventAsync(
        string accessToken, int tenantId, string projectCode, Guid eventId, long? expectedPreviousItemId, string summary,
        string actorType = "CODER", int actorId = 1, string eventType = "CLAIM",
        string? workStream = null, Guid? leaseId = null, long? expectedLeaseVersion = null)
    {
        var response = await PostEventRawAsync(
            accessToken, tenantId, projectCode, eventId, expectedPreviousItemId, summary,
            actorType: actorType, actorId: actorId, eventType: eventType,
            workStream: workStream, leaseId: leaseId, expectedLeaseVersion: expectedLeaseVersion);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var itemId = body.GetProperty("itemId").GetInt64();
        Guid? newLeaseId = null;
        long? newLeaseVersion = null;
        if (body.TryGetProperty("lease", out var lease) && lease.ValueKind != JsonValueKind.Null)
        {
            newLeaseId = lease.GetProperty("leaseId").GetGuid();
            newLeaseVersion = lease.GetProperty("version").GetInt64();
        }
        return (itemId, newLeaseId, newLeaseVersion);
    }

    // ---- Credenciales ----

    [SkippableFact]
    public async Task IssueToken_WrongSecret_ReturnsUnauthorized()
    {
        RequireDatabase(fixture);
        var (credentialId, _) = await fixture.CreateCredentialAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "CODER", "invalid-secret-test");

        var response = await fixture.RequestTokenRawAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, credentialId, Convert.ToBase64String(new byte[32]));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task IssueToken_RevokedCredential_ReturnsUnauthorized()
    {
        RequireDatabase(fixture);
        var (credentialId, secret) = await fixture.CreateCredentialAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "CODER", "revoked-credential-test");
        await fixture.RevokeCredentialAsync(ProjectControlDatabaseFixture.PrimaryTenantId, credentialId);

        var response = await fixture.RequestTokenRawAsync(ProjectControlDatabaseFixture.PrimaryTenantId, credentialId, secret);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task RegisterEvent_ExpiredToken_ReturnsUnauthorized()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "EXPIREDTOKEN", "Token expirado");

        // Mismo esquema de firma que JwtAccessTokenIssuer, pero con "expires" en el pasado -
        // simula sin esperar el vencimiento real de un token emitido normalmente.
        var keyBytes = JwtAccessTokenIssuer.ResolveSigningKeyBytes("DEV-ONLY-INSECURE-PLACEHOLDER-KEY-NOT-FOR-PRODUCTION-32-BYTES-MIN");
        var credentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
        var expiredToken = new JwtSecurityToken(
            issuer: "aura-api", audience: "aura-agents",
            claims:
            [
                new Claim(AuraJwtClaimTypes.TenantId, ProjectControlDatabaseFixture.PrimaryTenantId.ToString()),
                new Claim(AuraJwtClaimTypes.CredentialId, "1"),
                new Claim(AuraJwtClaimTypes.ActorType, "CODER"),
                new Claim(AuraJwtClaimTypes.AgentName, "expired-test"),
            ],
            expires: DateTime.UtcNow.AddMinutes(-5),
            signingCredentials: credentials);
        var expiredTokenText = new JwtSecurityTokenHandler().WriteToken(expiredToken);

        var response = await PostEventRawAsync(
            expiredTokenText, ProjectControlDatabaseFixture.PrimaryTenantId, "EXPIREDTOKEN", Guid.NewGuid(), null, "No debería aplicarse");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [SkippableFact]
    public async Task RegisterEvent_ActorTypeSpoofing_IsRejectedAsForbidden()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "SPOOFTEST", "Spoofing rechazado");

        // Token autenticado como CODER; el cuerpo dice ser HUMAN (el rol HUMAN-only de
        // REQUEST/APPROVAL no debe poder auto-otorgarse asi).
        var coderToken = fixture.GetBearerToken(ProjectControlDatabaseFixture.PrimaryTenantId, "CODER");

        var response = await PostEventRawAsync(
            coderToken, ProjectControlDatabaseFixture.PrimaryTenantId, "SPOOFTEST", Guid.NewGuid(), null,
            "Intento de actuar como HUMAN con credencial CODER", actorType: "HUMAN", eventType: "CLAIM");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [SkippableFact]
    public async Task RegisterEvent_TokenFromOtherTenant_IsRejectedAsForbidden()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.SecondaryTenantId, "TENANTTOKEN", "Token de otro tenant");

        var primaryTenantToken = fixture.GetBearerToken(ProjectControlDatabaseFixture.PrimaryTenantId, "CODER");

        // El token es del tenant primario; el evento declara el tenant secundario - debe
        // rechazarse aunque el proyecto exista en ese tenant.
        var response = await PostEventRawAsync(
            primaryTenantToken, ProjectControlDatabaseFixture.SecondaryTenantId, "TENANTTOKEN", Guid.NewGuid(), null,
            "No debería poder cruzar tenants");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---- Concurrencia: lease ----

    [SkippableFact]
    public async Task RegisterEvent_SecondClaimByDifferentCredential_ReturnsLeaseConflict()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "TWOAGENTS", "Dos agentes, misma unidad");

        var tokenA = fixture.GetBearerToken(ProjectControlDatabaseFixture.PrimaryTenantId, "CODER");
        var (credentialB, secretB) = await fixture.CreateCredentialAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "CODER", "Agent B");
        var tokenB = await fixture.MintTokenAsync(ProjectControlDatabaseFixture.PrimaryTenantId, credentialB, secretB);

        var claimA = await PostEventAsync(
            tokenA, ProjectControlDatabaseFixture.PrimaryTenantId, "TWOAGENTS", Guid.NewGuid(), null, "Agent A toma el control");

        // Agent B (credencial distinta) intenta tomar la MISMA unidad ya controlada por A.
        var claimBResponse = await PostEventRawAsync(
            tokenB, ProjectControlDatabaseFixture.PrimaryTenantId, "TWOAGENTS", Guid.NewGuid(), claimA.ItemId, "Agent B intenta tomar el control");

        Assert.Equal(HttpStatusCode.Conflict, claimBResponse.StatusCode);
        var body = await claimBResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LEASE_CONFLICT", body.GetProperty("status").GetString());
    }

    [SkippableFact]
    public async Task RegisterEvent_TwoIndependentWorkstreams_ProceedWithoutBlockingEachOther()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "MULTIWORKSTREAM", "Workstreams independientes");

        var tokenA = fixture.GetBearerToken(ProjectControlDatabaseFixture.PrimaryTenantId, "CODER");
        var (credentialB, secretB) = await fixture.CreateCredentialAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "CODER", "Agent B (workstream Y)");
        var tokenB = await fixture.MintTokenAsync(ProjectControlDatabaseFixture.PrimaryTenantId, credentialB, secretB);

        // Agent A trabaja en Workstream X, Agent B en Workstream Y del MISMO proyecto -
        // ninguno debe bloquear al otro (AURA-PROTOCOL-V2-01, ejemplo obligatorio de concurrencia).
        var claimA = await PostEventAsync(
            tokenA, ProjectControlDatabaseFixture.PrimaryTenantId, "MULTIWORKSTREAM", Guid.NewGuid(), null,
            "Agent A toma Workstream X", workStream: "X");
        var claimB = await PostEventAsync(
            tokenB, ProjectControlDatabaseFixture.PrimaryTenantId, "MULTIWORKSTREAM", Guid.NewGuid(), null,
            "Agent B toma Workstream Y", workStream: "Y");

        Assert.NotEqual(claimA.LeaseId, claimB.LeaseId);

        // Ambos progresan sin conflicto, cada uno con su propio lease/workstream.
        var progressAResponse = await PostEventRawAsync(
            tokenA, ProjectControlDatabaseFixture.PrimaryTenantId, "MULTIWORKSTREAM", Guid.NewGuid(), claimA.ItemId,
            "Agent A avanza en X", eventType: "PROGRESS", workStream: "X",
            leaseId: claimA.LeaseId, expectedLeaseVersion: claimA.LeaseVersion);
        var progressBResponse = await PostEventRawAsync(
            tokenB, ProjectControlDatabaseFixture.PrimaryTenantId, "MULTIWORKSTREAM", Guid.NewGuid(), claimB.ItemId,
            "Agent B avanza en Y", eventType: "PROGRESS", workStream: "Y",
            leaseId: claimB.LeaseId, expectedLeaseVersion: claimB.LeaseVersion);

        Assert.Equal(HttpStatusCode.Created, progressAResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, progressBResponse.StatusCode);
    }

    [SkippableFact]
    public async Task RegisterEvent_ExpiredLease_ReturnsLeaseConflictNotSilentAcceptance()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        var projectId = await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "EXPIREDLEASE", "Lease vencido");

        var token = fixture.GetBearerToken(ProjectControlDatabaseFixture.PrimaryTenantId, "CODER");
        var claim = await PostEventAsync(
            token, ProjectControlDatabaseFixture.PrimaryTenantId, "EXPIREDLEASE", Guid.NewGuid(), null, "Toma el control");

        await fixture.ExpireLeaseAsync(ProjectControlDatabaseFixture.PrimaryTenantId, projectId);

        var progressResponse = await PostEventRawAsync(
            token, ProjectControlDatabaseFixture.PrimaryTenantId, "EXPIREDLEASE", Guid.NewGuid(), claim.ItemId,
            "Intento de avanzar con lease vencido", eventType: "PROGRESS",
            leaseId: claim.LeaseId, expectedLeaseVersion: claim.LeaseVersion);

        Assert.Equal(HttpStatusCode.Conflict, progressResponse.StatusCode);
        var body = await progressResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LEASE_CONFLICT", body.GetProperty("status").GetString());
    }

    [SkippableFact]
    public async Task RegisterEvent_StaleExpectedLeaseVersion_ReturnsLeaseConflict()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "STALEVERSION", "Version de lease obsoleta");

        var token = fixture.GetBearerToken(ProjectControlDatabaseFixture.PrimaryTenantId, "CODER");
        var claim = await PostEventAsync(
            token, ProjectControlDatabaseFixture.PrimaryTenantId, "STALEVERSION", Guid.NewGuid(), null, "Toma el control");

        var response = await PostEventRawAsync(
            token, ProjectControlDatabaseFixture.PrimaryTenantId, "STALEVERSION", Guid.NewGuid(), claim.ItemId,
            "Version incorrecta", eventType: "PROGRESS",
            leaseId: claim.LeaseId, expectedLeaseVersion: (claim.LeaseVersion ?? 0) + 99);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LEASE_CONFLICT", body.GetProperty("status").GetString());
    }

    [SkippableFact]
    public async Task RegisterEvent_LeaseRenewal_IncrementsVersionEachAcceptedWrite()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "LEASERENEW", "Renovacion de lease");

        var token = fixture.GetBearerToken(ProjectControlDatabaseFixture.PrimaryTenantId, "CODER");
        var claim = await PostEventAsync(
            token, ProjectControlDatabaseFixture.PrimaryTenantId, "LEASERENEW", Guid.NewGuid(), null, "Toma el control");
        Assert.Equal(1, claim.LeaseVersion);

        var progress = await PostEventAsync(
            token, ProjectControlDatabaseFixture.PrimaryTenantId, "LEASERENEW", Guid.NewGuid(), claim.ItemId,
            "Avance 1", eventType: "PROGRESS", leaseId: claim.LeaseId, expectedLeaseVersion: claim.LeaseVersion);
        Assert.Equal(claim.LeaseId, progress.LeaseId);
        Assert.Equal(2, progress.LeaseVersion);
    }
}
