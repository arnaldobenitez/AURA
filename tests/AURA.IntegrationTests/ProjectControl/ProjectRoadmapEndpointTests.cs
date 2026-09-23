using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AURA.Application.ProjectControl;

namespace AURA.IntegrationTests.ProjectControl;

[Collection(ProjectControlDatabaseCollection.Name)]
public sealed class ProjectRoadmapEndpointTests(ProjectControlDatabaseFixture fixture)
{
    private sealed record EventResponse(HttpStatusCode StatusCode, long? ItemId, Guid? LeaseId, long? LeaseVersion);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [SkippableFact]
    public async Task Roadmap_DesignatedAiAnalystCanApproveWithWarningsAndCountsAsClosed()
    {
        Skip.IfNot(fixture.IsAvailable, "Requiere conectividad con SQL Server de desarrollo.");
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "ROADMAP", "Roadmap de prueba");

        var tenantId = ProjectControlDatabaseFixture.PrimaryTenantId;
        const string projectCode = "ROADMAP";
        var claim = await PostEventAsync(
            projectCode, null, "CLAIM", "Iniciar trabajo", "CODER", stage: null);

        var planRequest = new UpdateProjectRoadmapRequest(
            tenantId,
            null,
            Guid.NewGuid(),
            [new ProjectRoadmapStageDefinition("SMT-00", "Baseline", 1, "Build y tests correctos", fixture.GetCredentialId(tenantId, "AI_ANALYST"))]);
        using var planMessage = new HttpRequestMessage(HttpMethod.Put, $"/api/project-control/projects/{projectCode}/roadmap")
        {
            Content = JsonContent.Create(planRequest, options: JsonOptions),
        };
        fixture.AddBearer(planMessage, tenantId, "HUMAN");
        var planResponse = await fixture.Client.SendAsync(planMessage);
        Assert.Equal(HttpStatusCode.OK, planResponse.StatusCode);

        var initialRoadmap = await fixture.Client.GetFromJsonAsync<ProjectRoadmapDto>(
            $"/api/project-control/projects/{projectCode}/roadmap?tenantId={tenantId}", JsonOptions);
        Assert.NotNull(initialRoadmap);
        Assert.Equal(1, initialRoadmap!.TotalStages);
        Assert.Equal("PENDING", Assert.Single(initialRoadmap.Stages).Status);

        var progress = await PostEventAsync(
            projectCode, claim.ItemId, "PROGRESS", "Implementación lista", "CODER",
            stage: "Baseline", stageCode: "SMT-00", stageProgress: 100m,
            leaseId: claim.LeaseId, leaseVersion: claim.LeaseVersion);

        var (otherCredentialId, otherSecret) = await fixture.CreateCredentialAsync(tenantId, "AI_ANALYST", "unassigned-reviewer");
        var otherToken = await fixture.MintTokenAsync(tenantId, otherCredentialId, otherSecret);
        var rejected = await PostEventAsync(
            projectCode, progress.ItemId, "APPROVAL", "Intento de revisor no asignado", "AI_ANALYST",
            stage: "Baseline", stageCode: "SMT-00", stageOutcome: "PASS_WITH_WARNINGS", bearerToken: otherToken);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        var approval = await PostEventAsync(
            projectCode, progress.ItemId, "APPROVAL", "Validado con observaciones no bloqueantes", "AI_ANALYST",
            stage: "Baseline", stageCode: "SMT-00", stageOutcome: "PASS_WITH_WARNINGS");
        Assert.Equal(HttpStatusCode.Created, approval.StatusCode);

        var roadmap = await fixture.Client.GetFromJsonAsync<ProjectRoadmapDto>(
            $"/api/project-control/projects/{projectCode}/roadmap?tenantId={tenantId}", JsonOptions);
        Assert.NotNull(roadmap);
        Assert.Equal(100m, roadmap!.ExecProgress);
        Assert.Equal(1, roadmap.ClosedStages);
        var stage = Assert.Single(roadmap.Stages);
        Assert.Equal("PASS_WITH_WARNINGS", stage.Status);
        Assert.Equal("PASS_WITH_WARNINGS", stage.ApprovalOutcome);
        Assert.Equal("test-AI_ANALYST", stage.ValidatorName);
        Assert.Equal("Validado con observaciones no bloqueantes", stage.ApprovalSummary);

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={tenantId}", JsonOptions);
        var project = Assert.Single(board!.Projects);
        Assert.Equal(1, project.ClosedStages);
        Assert.Equal(100m, project.ExecProgress);

        var rework = await PostEventAsync(
            projectCode, approval.ItemId, "REWORK", "Reabrir por una nueva observación", "HUMAN",
            stage: "Baseline", stageCode: "SMT-00");
        Assert.Equal(HttpStatusCode.Created, rework.StatusCode);
        var reopenedRoadmap = await fixture.Client.GetFromJsonAsync<ProjectRoadmapDto>(
            $"/api/project-control/projects/{projectCode}/roadmap?tenantId={tenantId}", JsonOptions);
        var reopenedStage = Assert.Single(reopenedRoadmap!.Stages);
        Assert.Equal("PENDING", reopenedStage.Status);
        Assert.Equal(0m, reopenedStage.Progress);
        Assert.Equal(0, reopenedRoadmap.ClosedStages);
        Assert.Equal(0m, reopenedRoadmap.ExecProgress);
    }

    [SkippableFact]
    public async Task Roadmap_PlanRequiresHumanAndCannotOmitExistingStages()
    {
        Skip.IfNot(fixture.IsAvailable, "Requiere conectividad con SQL Server de desarrollo.");
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "ROADMAPLOCK", "Roadmap protegido");
        var tenantId = ProjectControlDatabaseFixture.PrimaryTenantId;
        await PostEventAsync("ROADMAPLOCK", null, "CLAIM", "Abrir EXEC", "CODER", stage: null);

        var plan = new UpdateProjectRoadmapRequest(
            tenantId, null, Guid.NewGuid(),
            [new ProjectRoadmapStageDefinition("A-1", "Análisis", 1, null, fixture.GetCredentialId(tenantId, "AI_ANALYST"))]);
        using var forbidden = new HttpRequestMessage(HttpMethod.Put, "/api/project-control/projects/ROADMAPLOCK/roadmap")
        {
            Content = JsonContent.Create(plan, options: JsonOptions),
        };
        fixture.AddBearer(forbidden, tenantId, "CODER");
        Assert.Equal(HttpStatusCode.Forbidden, (await fixture.Client.SendAsync(forbidden)).StatusCode);

        using var accepted = new HttpRequestMessage(HttpMethod.Put, "/api/project-control/projects/ROADMAPLOCK/roadmap")
        {
            Content = JsonContent.Create(plan, options: JsonOptions),
        };
        fixture.AddBearer(accepted, tenantId, "HUMAN");
        Assert.Equal(HttpStatusCode.OK, (await fixture.Client.SendAsync(accepted)).StatusCode);

        var omission = new UpdateProjectRoadmapRequest(tenantId, null, Guid.NewGuid(),
            [new ProjectRoadmapStageDefinition("B-1", "Reemplazo no permitido", 1, null, fixture.GetCredentialId(tenantId, "AI_ANALYST"))]);
        using var update = new HttpRequestMessage(HttpMethod.Put, "/api/project-control/projects/ROADMAPLOCK/roadmap")
        {
            Content = JsonContent.Create(omission, options: JsonOptions),
        };
        fixture.AddBearer(update, tenantId, "HUMAN");
        Assert.Equal(HttpStatusCode.Conflict, (await fixture.Client.SendAsync(update)).StatusCode);
    }

    private async Task<EventResponse> PostEventAsync(
        string projectCode,
        long? previousItemId,
        string eventType,
        string summary,
        string actorType,
        string? stage,
        string? stageCode = null,
        decimal? stageProgress = null,
        string? stageOutcome = null,
        Guid? leaseId = null,
        long? leaseVersion = null,
        string? bearerToken = null)
    {
        var tenantId = ProjectControlDatabaseFixture.PrimaryTenantId;
        var isAgent = actorType is "CODER" or "AI_ANALYST";
        var request = new RegisterProjectEventRequest(
            tenantId,
            projectCode,
            Guid.NewGuid(),
            previousItemId,
            eventType,
            new ProjectActorRefDto(actorType, actorType == "AI_ANALYST" ? 2 : 1),
            isAgent ? "test-agent" : null,
            isAgent ? "integration-test" : null,
            "IN_PROGRESS",
            summary,
            null,
            stage,
            stageProgress,
            null,
            null,
            null,
            null,
            null,
            isAgent ? "test-model" : null,
            null,
            leaseId,
            leaseVersion,
            stageCode,
            stageOutcome);
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/project-control/events")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        if (bearerToken is null)
        {
            fixture.AddBearer(message, tenantId, actorType);
        }
        else
        {
            fixture.AddBearer(message, bearerToken);
        }

        var response = await fixture.Client.SendAsync(message);
        if (!response.IsSuccessStatusCode)
        {
            return new EventResponse(response.StatusCode, null, null, null);
        }

        var body = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var lease = body.TryGetProperty("lease", out var leaseJson) && leaseJson.ValueKind == JsonValueKind.Object
            ? (leaseJson.GetProperty("leaseId").GetGuid(), (long?)leaseJson.GetProperty("version").GetInt64())
            : ((Guid?, long?))(null, null);
        return new EventResponse(response.StatusCode, body.GetProperty("itemId").GetInt64(), lease.Item1, lease.Item2);
    }
}
