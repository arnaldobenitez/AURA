using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AURA.Api.Admin;
using AURA.Application.Registration;

namespace AURA.IntegrationTests.ProjectControl;

[Collection(ProjectControlDatabaseCollection.Name)]
public sealed class AdminManagementEndpointTests(ProjectControlDatabaseFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static void RequireDatabase(ProjectControlDatabaseFixture fixture) =>
        Skip.IfNot(fixture.IsAvailable, "Requiere conectividad con SQL Server de desarrollo.");

    [SkippableFact]
    public async Task AdminEndpoints_RequireAuthenticationAndTenantAdminRole()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();

        using var anonymous = fixture.Factory.CreateClient();
        var anonymousResponse = await anonymous.GetAsync($"/api/admin/projects?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        var nonAdmin = await fixture.Client.GetAsync($"/api/admin/projects?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}");
        Assert.Equal(HttpStatusCode.Forbidden, nonAdmin.StatusCode);
        using var nonAdminProjectCreate = new HttpRequestMessage(HttpMethod.Post, "/api/project-control/projects")
        {
            Content = JsonContent.Create(new { tenantId = ProjectControlDatabaseFixture.PrimaryTenantId, projectCode = "DENIED", projectName = "Denied" }, options: JsonOptions),
        };
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.SendAsync(nonAdminProjectCreate)).StatusCode);

        using var crossTenant = new HttpRequestMessage(HttpMethod.Get, $"/api/admin/projects?tenantId={ProjectControlDatabaseFixture.SecondaryTenantId}");
        fixture.AddBearer(crossTenant, fixture.GetPortalAdminToken(ProjectControlDatabaseFixture.PrimaryTenantId, 1));
        var crossTenantResponse = await fixture.Client.SendAsync(crossTenant);
        Assert.Equal(HttpStatusCode.Forbidden, crossTenantResponse.StatusCode);
    }

    [SkippableFact]
    public async Task ProjectCrud_RejectsDuplicateCodeAndNeverDeletesProject()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();

        var created = await AdminRequestAsync(HttpMethod.Post, "/api/admin/projects", new
        {
            tenantId = ProjectControlDatabaseFixture.PrimaryTenantId,
            projectCode = "SMT",
            projectName = "SMT",
            description = "Project scope test",
            externalProjectKey = "external-smt",
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var project = await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var projectId = project.GetProperty("projectId").GetInt32();

        var duplicate = await AdminRequestAsync(HttpMethod.Post, "/api/admin/projects", new
        {
            tenantId = ProjectControlDatabaseFixture.PrimaryTenantId,
            projectCode = "smt",
            projectName = "Duplicate",
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var pause = await AdminRequestAsync(HttpMethod.Post, $"/api/admin/projects/{projectId}/pause?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}");
        Assert.Equal(HttpStatusCode.OK, pause.StatusCode);
        var activate = await AdminRequestAsync(HttpMethod.Post, $"/api/admin/projects/{projectId}/activate?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}");
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
        var deactivate = await AdminRequestAsync(HttpMethod.Post, $"/api/admin/projects/{projectId}/deactivate?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}");
        Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);

        var list = await AdminRequestAsync(HttpMethod.Get, $"/api/admin/projects?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}");
        var projects = await list.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.Contains(projects.EnumerateArray(), row => row.GetProperty("projectId").GetInt32() == projectId && row.GetProperty("status").GetString() == "ARCHIVED");
        var reactivate = await AdminRequestAsync(HttpMethod.Post, $"/api/admin/projects/{projectId}/activate?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}");
        Assert.Equal(HttpStatusCode.OK, reactivate.StatusCode);
    }

    [SkippableFact]
    public async Task IngestionCredential_IsProjectScoped_SecretIsOneTime_AndRevocationIsImmediate()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        var smtProjectId = await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "SMT", "SMT");
        var otherProjectId = await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "OTHER", "Other");
        var logStart = fixture.CapturedLogCount;

        var created = await AdminRequestAsync(HttpMethod.Post, "/api/admin/agent-credentials", new
        {
            tenantId = ProjectControlDatabaseFixture.PrimaryTenantId,
            agentName = "SMT-DEVIN",
            actorType = "INGESTION",
            projectIds = new[] { smtProjectId },
            expiresAtUtc = (DateTime?)null,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var initial = await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var credentialId = initial.GetProperty("credentialId").GetInt32();
        var secret = initial.GetProperty("secret").GetString();
        Assert.False(string.IsNullOrWhiteSpace(secret));
        var secretWasLogged = fixture.CapturedLogsSince(logStart).Any(message => message.Contains(secret!, StringComparison.Ordinal));
        Assert.False(secretWasLogged, "The generated credential secret appeared in application logs.");

        var metadataResponse = await AdminRequestAsync(HttpMethod.Get, $"/api/admin/agent-credentials?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}");
        var metadataJson = await metadataResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(secret!, metadataJson, StringComparison.Ordinal);
        var metadata = JsonDocument.Parse(metadataJson).RootElement;
        var listed = Assert.Single(metadata.EnumerateArray(), row => row.GetProperty("credentialId").GetInt32() == credentialId);
        Assert.False(listed.TryGetProperty("secret", out _));
        Assert.Equal("PROJECT_SCOPED", listed.GetProperty("scopeMode").GetString());
        var duplicateAssignment = await AdminRequestAsync(HttpMethod.Post, $"/api/admin/agent-credentials/{credentialId}/projects", new
        {
            tenantId = ProjectControlDatabaseFixture.PrimaryTenantId,
            projectId = smtProjectId,
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicateAssignment.StatusCode);

        var token = await fixture.MintTokenAsync(ProjectControlDatabaseFixture.PrimaryTenantId, credentialId, secret!);
        var allowed = await RegisterAsync(token, ProjectControlDatabaseFixture.PrimaryTenantId, "SMT");
        Assert.Equal(HttpStatusCode.Created, allowed.StatusCode);
        var denied = await RegisterAsync(token, ProjectControlDatabaseFixture.PrimaryTenantId, "OTHER");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var revoke = await AdminRequestAsync(HttpMethod.Post, $"/api/admin/agent-credentials/{credentialId}/revoke?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}");
        Assert.Equal(HttpStatusCode.OK, revoke.StatusCode);
        var oldTokenWrite = await RegisterAsync(token, ProjectControlDatabaseFixture.PrimaryTenantId, "SMT");
        Assert.Equal(HttpStatusCode.Forbidden, oldTokenWrite.StatusCode);
        var futureToken = await fixture.RequestTokenRawAsync(ProjectControlDatabaseFixture.PrimaryTenantId, credentialId, secret!);
        Assert.Equal(HttpStatusCode.Unauthorized, futureToken.StatusCode);
    }

    [SkippableFact]
    public async Task RotationRevokesOldCredentialAndCopiesProjectScope()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        var projectId = await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "SMTROT", "SMT rotation");
        var created = await AdminRequestAsync(HttpMethod.Post, "/api/admin/agent-credentials", new
        {
            tenantId = ProjectControlDatabaseFixture.PrimaryTenantId,
            agentName = "SMT-DEVIN",
            actorType = "INGESTION",
            projectIds = new[] { projectId },
        });
        var first = await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var oldId = first.GetProperty("credentialId").GetInt32();
        var oldSecret = first.GetProperty("secret").GetString()!;
        var oldToken = await fixture.MintTokenAsync(ProjectControlDatabaseFixture.PrimaryTenantId, oldId, oldSecret);

        var rotated = await AdminRequestAsync(HttpMethod.Post, $"/api/admin/agent-credentials/{oldId}/rotate?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}");
        Assert.Equal(HttpStatusCode.Created, rotated.StatusCode);
        var replacement = await rotated.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var newId = replacement.GetProperty("credentialId").GetInt32();
        Assert.NotEqual(oldId, newId);
        var listedAfterRotation = await AdminRequestAsync(HttpMethod.Get, $"/api/admin/agent-credentials?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}");
        var rowsAfterRotation = await listedAfterRotation.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var newMetadata = Assert.Single(rowsAfterRotation.EnumerateArray(), row => row.GetProperty("credentialId").GetInt32() == newId);
        Assert.Equal(oldId, newMetadata.GetProperty("rotatedFromCredentialId").GetInt32());
        Assert.Equal(projectId, Assert.Single(replacement.GetProperty("projects").EnumerateArray()).GetProperty("projectId").GetInt32());
        Assert.Equal(HttpStatusCode.Unauthorized, (await fixture.RequestTokenRawAsync(ProjectControlDatabaseFixture.PrimaryTenantId, oldId, oldSecret)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await RegisterAsync(oldToken, ProjectControlDatabaseFixture.PrimaryTenantId, "SMTROT")).StatusCode);

        var newToken = await fixture.MintTokenAsync(ProjectControlDatabaseFixture.PrimaryTenantId, newId, replacement.GetProperty("secret").GetString()!);
        Assert.Equal(HttpStatusCode.Created, (await RegisterAsync(newToken, ProjectControlDatabaseFixture.PrimaryTenantId, "SMTROT")).StatusCode);
    }

    [SkippableFact]
    public async Task LegacyCredentialMustBeProjectScopedBeforeRotation()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        var allowedProjectId = await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "LEGACY-SMT", "Legacy migration scope");
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "LEGACY-OTHER", "Other legacy project");
        var legacy = await fixture.CreateLegacyIngestionCredentialAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "legacy-ingestion");

        var blockedRotation = await AdminRequestAsync(HttpMethod.Post, $"/api/admin/agent-credentials/{legacy.CredentialId}/rotate?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}");
        Assert.Equal(HttpStatusCode.Conflict, blockedRotation.StatusCode);

        var assignment = await AdminRequestAsync(HttpMethod.Post, $"/api/admin/agent-credentials/{legacy.CredentialId}/projects", new
        {
            tenantId = ProjectControlDatabaseFixture.PrimaryTenantId,
            projectId = allowedProjectId,
        });
        Assert.Equal(HttpStatusCode.Created, assignment.StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await RegisterAsync(legacy.Token, ProjectControlDatabaseFixture.PrimaryTenantId, "LEGACY-SMT")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await RegisterAsync(legacy.Token, ProjectControlDatabaseFixture.PrimaryTenantId, "LEGACY-OTHER")).StatusCode);

        var rotated = await AdminRequestAsync(HttpMethod.Post, $"/api/admin/agent-credentials/{legacy.CredentialId}/rotate?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}");
        Assert.Equal(HttpStatusCode.Created, rotated.StatusCode);
    }

    [SkippableFact]
    public async Task UnassigningProjectImmediatelyBlocksExistingToken()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        var projectId = await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "SMTUNASSIGN", "SMT unassign");
        var created = await AdminRequestAsync(HttpMethod.Post, "/api/admin/agent-credentials", new
        {
            tenantId = ProjectControlDatabaseFixture.PrimaryTenantId,
            agentName = "SMT-CLAUDE-CODE",
            actorType = "INGESTION",
            projectIds = new[] { projectId },
        });
        var credential = await created.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var token = await fixture.MintTokenAsync(ProjectControlDatabaseFixture.PrimaryTenantId,
            credential.GetProperty("credentialId").GetInt32(), credential.GetProperty("secret").GetString()!);

        var revoke = await AdminRequestAsync(HttpMethod.Delete,
            $"/api/admin/agent-credentials/{credential.GetProperty("credentialId").GetInt32()}/projects/{projectId}?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}");
        Assert.Equal(HttpStatusCode.NoContent, revoke.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await RegisterAsync(token, ProjectControlDatabaseFixture.PrimaryTenantId, "SMTUNASSIGN")).StatusCode);
    }

    private async Task<HttpResponseMessage> AdminRequestAsync(HttpMethod method, string path, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Admin-Api-Key", AdminApiKeyFilter.DevelopmentOnlyPlaceholder);
        if (body is not null) request.Content = JsonContent.Create(body, options: JsonOptions);
        return await fixture.Client.SendAsync(request);
    }

    private async Task<HttpResponseMessage> RegisterAsync(string token, int tenantId, string projectCode)
    {
        var payload = new RegisterRegistrationEventRequest(
            Guid.NewGuid(), tenantId, projectCode, "REGISTRATION_ONLY", "PROGRESS", "Admin scope test",
            null, null, null, "test-agent", null, null, null, null, "Development", "integration-test",
            DateTimeOffset.UtcNow, null, null, "UNVERIFIED");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/agent-registration/events")
        {
            Content = JsonContent.Create(payload, options: JsonOptions),
        };
        fixture.AddBearer(request, token);
        return await fixture.Client.SendAsync(request);
    }
}
