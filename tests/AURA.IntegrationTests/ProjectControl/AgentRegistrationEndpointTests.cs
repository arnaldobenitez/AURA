using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AURA.Application.ProjectControl;
using AURA.Application.Registration;

namespace AURA.IntegrationTests.ProjectControl;

/// <summary>
/// AURA Agent Registration Protocol v2.3 integration coverage. The tests prove the append-only
/// registration channel works through HTTP and a real SQL Server while leaving all governed
/// ProjectControl tables/state untouched.
/// </summary>
[Collection(ProjectControlDatabaseCollection.Name)]
public sealed class AgentRegistrationEndpointTests(ProjectControlDatabaseFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static void RequireDatabase(ProjectControlDatabaseFixture fixture)
    {
        Skip.IfNot(fixture.IsAvailable, "Requiere conectividad con el SQL Server de desarrollo (AURA_DESIGNTIME_CONNSTR o User Secrets de AURA.Api).");
    }

    [SkippableFact]
    public async Task Register_ValidEvent_PersistsAllFieldsAndReadBackReturnsThem()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        var projectId = await fixture.CreateProjectAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "REGVALID", "Registration válida");
        var eventId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var occurredAtUtc = new DateTimeOffset(2026, 9, 27, 12, 34, 56, TimeSpan.Zero);
        var request = CreateRequest(eventId, "REGVALID") with
        {
            ClaimedActorType = "CODER",
            ClaimedActorId = 7,
            ExternalProjectKey = "external-regvalid",
            ProjectName = "Registration válida",
            SessionId = sessionId,
            OccurredAtUtc = occurredAtUtc,
        };

        var response = await PostRegistrationAsync(request);
        var created = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("CREATED", created.GetProperty("status").GetString());
        Assert.Equal(eventId, created.GetProperty("eventId").GetGuid());
        var itemId = created.GetProperty("itemId").GetInt64();
        Assert.True(itemId > 0);
        Assert.Equal("RESOLVED", created.GetProperty("projectResolution").GetString());

        var readBack = await GetRegistrationAsync(eventId, ProjectControlDatabaseFixture.PrimaryTenantId);
        Assert.Equal(HttpStatusCode.OK, readBack.StatusCode);
        var persisted = await ReadJsonAsync(readBack);

        Assert.Equal(itemId, persisted.GetProperty("itemId").GetInt64());
        Assert.Equal(eventId, persisted.GetProperty("eventId").GetGuid());
        Assert.Equal(ProjectControlDatabaseFixture.PrimaryTenantId, persisted.GetProperty("tenantId").GetInt32());
        Assert.Equal(projectId, persisted.GetProperty("projectId").GetInt32());
        Assert.Equal("REGVALID", persisted.GetProperty("projectCode").GetString());
        Assert.Equal("REGISTRATION_ONLY", persisted.GetProperty("mode").GetString());
        Assert.Equal("PROGRESS", persisted.GetProperty("eventType").GetString());
        Assert.Equal("Registered progress", persisted.GetProperty("summary").GetString());
        Assert.Equal("Details", persisted.GetProperty("details").GetString());
        Assert.Equal("CODER", persisted.GetProperty("claimedActorType").GetString());
        Assert.Equal(7, persisted.GetProperty("claimedActorId").GetInt32());
        Assert.Equal("agent", persisted.GetProperty("agentName").GetString());
        Assert.Equal("host", persisted.GetProperty("host").GetString());
        Assert.Equal("workspace", persisted.GetProperty("workspace").GetString());
        Assert.Equal(sessionId, persisted.GetProperty("sessionId").GetGuid());
        Assert.Equal("model", persisted.GetProperty("model").GetString());
        Assert.Equal("Development", persisted.GetProperty("environment").GetString());
        Assert.Equal("integration-test", persisted.GetProperty("source").GetString());
        Assert.Equal("external-regvalid", persisted.GetProperty("externalProjectKey").GetString());
        Assert.Equal("Registration válida", persisted.GetProperty("projectName").GetString());
        Assert.Equal("UNVERIFIED", persisted.GetProperty("identityStatus").GetString());
        Assert.Equal("RESOLVED", persisted.GetProperty("projectResolution").GetString());
    }

    [SkippableFact]
    public async Task Register_DuplicateEventId_ReturnsSameItemWithoutSecondInsert()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "REGDUP", "Registration duplicada");
        var eventId = Guid.NewGuid();
        var request = CreateRequest(eventId, "REGDUP");

        var first = await PostRegistrationAsync(request);
        var second = await PostRegistrationAsync(request with { Summary = "Changed summary must not replace the first row" });
        var firstBody = await ReadJsonAsync(first);
        var secondBody = await ReadJsonAsync(second);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal("ALREADY_EXISTS", secondBody.GetProperty("status").GetString());
        Assert.Equal(firstBody.GetProperty("itemId").GetInt64(), secondBody.GetProperty("itemId").GetInt64());
        Assert.Equal(firstBody.GetProperty("eventId").GetGuid(), secondBody.GetProperty("eventId").GetGuid());

        var readBack = await GetRegistrationAsync(eventId, ProjectControlDatabaseFixture.PrimaryTenantId);
        var persisted = await ReadJsonAsync(readBack);
        Assert.Equal("Registered progress", persisted.GetProperty("summary").GetString());
    }

    [SkippableFact]
    public async Task Register_UnknownProject_IsStoredAsProvisionalWithoutGovernedProject()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        var eventId = Guid.NewGuid();
        var request = CreateRequest(eventId, "NOT-YET-REGISTERED") with
        {
            ExternalProjectKey = "consumer-key-1",
            ProjectName = "Future project",
        };
        var legacyCredential = await fixture.CreateLegacyIngestionCredentialAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "legacy-registration-test");

        var response = await PostRegistrationAsync(request, ProjectControlDatabaseFixture.PrimaryTenantId, legacyCredential.Token);
        var body = await ReadJsonAsync(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("PROVISIONAL", body.GetProperty("projectResolution").GetString());

        var readBack = await GetRegistrationAsync(eventId, ProjectControlDatabaseFixture.PrimaryTenantId);
        var persisted = await ReadJsonAsync(readBack);
        Assert.Equal(JsonValueKind.Null, persisted.GetProperty("projectId").ValueKind);
        Assert.Equal("NOT-YET-REGISTERED", persisted.GetProperty("projectCode").GetString());
        Assert.Equal("consumer-key-1", persisted.GetProperty("externalProjectKey").GetString());
        Assert.Equal("Future project", persisted.GetProperty("projectName").GetString());
        Assert.Equal("PROVISIONAL", persisted.GetProperty("projectResolution").GetString());

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);
        Assert.Empty(board!.Projects);
    }

    [SkippableFact]
    public async Task Register_InvalidOrGovernedCredential_IsRejected()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        var assignedProjectId = await fixture.CreateProjectAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "REGWRONGSECRET", "Wrong secret scope");
        var (credentialId, _) = await fixture.CreateCredentialAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "INGESTION", "wrong-secret-test", [assignedProjectId]);
        var badTokenResponse = await fixture.RequestTokenRawAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, credentialId, Convert.ToBase64String(new byte[32]));
        Assert.Equal(HttpStatusCode.Unauthorized, badTokenResponse.StatusCode);

        var request = CreateRequest(Guid.NewGuid(), "REGFORBIDDEN");
        using var governedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/agent-registration/events")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        fixture.AddBearer(governedRequest, ProjectControlDatabaseFixture.PrimaryTenantId, "CODER");
        var governedResponse = await fixture.Client.SendAsync(governedRequest);

        Assert.Equal(HttpStatusCode.Forbidden, governedResponse.StatusCode);

        var governedEvent = new RegisterProjectEventRequest(
            ProjectControlDatabaseFixture.PrimaryTenantId,
            "REGFORBIDDEN",
            Guid.NewGuid(),
            null,
            "CLAIM",
            new ProjectActorRefDto("INGESTION", 1),
            "ingestion-agent",
            "test",
            "IN_PROGRESS",
            "Must not become governed control",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            "model");
        using var governedWrite = new HttpRequestMessage(HttpMethod.Post, "/api/project-control/events")
        {
            Content = JsonContent.Create(governedEvent, options: JsonOptions),
        };
        fixture.AddBearer(governedWrite, ProjectControlDatabaseFixture.PrimaryTenantId, "INGESTION");
        var governedWriteResponse = await fixture.Client.SendAsync(governedWrite);
        Assert.Equal(HttpStatusCode.Forbidden, governedWriteResponse.StatusCode);
    }

    [SkippableFact]
    public async Task Register_SecretMaterial_IsRejected()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        var request = CreateRequest(Guid.NewGuid(), "REGSECRET") with
        {
            Details = "Data Source=server;Initial Catalog=db;User ID=u;Password=p",
        };

        var response = await PostRegistrationAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("VALIDATION_FAILED", body.GetProperty("status").GetString());
    }

    [SkippableFact]
    public async Task Register_TenantIsolation_IsEnforcedForWriteAndReadBack()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        var eventId = Guid.NewGuid();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "TENANT1", "Tenant one");
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.SecondaryTenantId, "TENANT2", "Tenant two");

        using var crossTenantWrite = new HttpRequestMessage(HttpMethod.Post, "/api/agent-registration/events")
        {
            Content = JsonContent.Create(
                CreateRequest(eventId, "TENANT2") with { TenantId = ProjectControlDatabaseFixture.SecondaryTenantId },
                options: JsonOptions),
        };
        fixture.AddBearer(crossTenantWrite, ProjectControlDatabaseFixture.PrimaryTenantId, "INGESTION");
        var crossTenantResponse = await fixture.Client.SendAsync(crossTenantWrite);
        Assert.Equal(HttpStatusCode.Forbidden, crossTenantResponse.StatusCode);

        var created = await PostRegistrationAsync(
            CreateRequest(eventId, "TENANT1"),
            ProjectControlDatabaseFixture.PrimaryTenantId);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var crossTenantRead = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/agent-registration/events/{eventId}?tenantId={ProjectControlDatabaseFixture.SecondaryTenantId}");
        fixture.AddBearer(crossTenantRead, ProjectControlDatabaseFixture.SecondaryTenantId, "INGESTION");
        var crossTenantReadResponse = await fixture.Client.SendAsync(crossTenantRead);
        Assert.Equal(HttpStatusCode.Forbidden, crossTenantReadResponse.StatusCode);

        var secondTenant = await PostRegistrationAsync(
            CreateRequest(eventId, "TENANT2") with { TenantId = ProjectControlDatabaseFixture.SecondaryTenantId },
            ProjectControlDatabaseFixture.SecondaryTenantId);
        Assert.Equal(HttpStatusCode.Created, secondTenant.StatusCode);
    }

    [SkippableFact]
    public async Task Register_UnsupportedTypeOrGovernedMode_IsRejected()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();

        var badType = await PostRegistrationAsync(
            CreateRequest(Guid.NewGuid(), "REGTYPE") with { EventType = "CLAIM" });
        var governedMode = await PostRegistrationAsync(
            CreateRequest(Guid.NewGuid(), "REGMODE") with { Mode = "GOVERNED_EXECUTION" });

        Assert.Equal(HttpStatusCode.BadRequest, badType.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, governedMode.StatusCode);
    }

    [SkippableFact]
    public async Task Register_DoesNotAlterGovernedBoardOrExecutionState()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        var projectId = await fixture.CreateProjectAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "REGSAFE", "Registration aislada");
        var before = await fixture.GetGovernedRowCountsAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, projectId);

        var response = await PostRegistrationAsync(
            CreateRequest(Guid.NewGuid(), "REGSAFE") with
            {
                EventType = "BLOCKER",
                ClaimedActorType = "AI_ANALYST",
                Summary = "Informational blocker only",
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var after = await fixture.GetGovernedRowCountsAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, projectId);
        Assert.Equal(before, after);
        Assert.Equal((0, 0, 0, 0, 0), after);

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);
        var project = Assert.Single(board!.Projects, item => item.ProjectCode == "REGSAFE");
        Assert.Null(project.LastItemId);
        Assert.Null(project.LastEventType);
        Assert.Null(project.CurrentControlType);
        Assert.Null(project.ExecProgress);
        Assert.Null(project.StageProgress);
        Assert.Null(project.WorkStatus);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);

    private Task<HttpResponseMessage> PostRegistrationAsync(
        RegisterRegistrationEventRequest request,
        int tenantId = ProjectControlDatabaseFixture.PrimaryTenantId) =>
        PostRegistrationAsync((object)request, tenantId, null);

    private async Task<HttpResponseMessage> PostRegistrationAsync(object request, int tenantId, string? accessToken = null)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/agent-registration/events")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        if (accessToken is null) fixture.AddBearer(httpRequest, tenantId, "INGESTION");
        else fixture.AddBearer(httpRequest, accessToken);
        return await fixture.Client.SendAsync(httpRequest);
    }

    private async Task<HttpResponseMessage> GetRegistrationAsync(Guid eventId, int tenantId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/agent-registration/events/{eventId}?tenantId={tenantId}");
        fixture.AddBearer(request, tenantId, "INGESTION");
        return await fixture.Client.SendAsync(request);
    }

    private static RegisterRegistrationEventRequest CreateRequest(Guid eventId, string projectCode) => new(
        eventId,
        ProjectControlDatabaseFixture.PrimaryTenantId,
        projectCode,
        "REGISTRATION_ONLY",
        "PROGRESS",
        "Registered progress",
        "Details",
        null,
        null,
        "agent",
        "host",
        "workspace",
        null,
        "model",
        "Development",
        "integration-test",
        DateTimeOffset.UtcNow,
        null,
        null,
        "UNVERIFIED");
}
