using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AURA.Application.ProjectControl;

namespace AURA.IntegrationTests.ProjectControl;

[Collection(ProjectControlDatabaseCollection.Name)]
public sealed class ProjectHistoryEndpointTests(ProjectControlDatabaseFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static void RequireDatabase(ProjectControlDatabaseFixture fixture)
    {
        Skip.IfNot(fixture.IsAvailable, "Requiere conectividad con el SQL Server de desarrollo (AURA_DESIGNTIME_CONNSTR o User Secrets de AURA.Api).");
    }

    private Task<HttpResponseMessage> PostEventRawAsync(
        int tenantId, string projectCode, Guid eventId, long? expectedPreviousItemId, string summary,
        string actorType = "HUMAN", int actorId = 1, string? model = null,
        string? agent = null, string? environment = null, string eventType = "PROGRESS")
    {
        var request = new RegisterProjectEventRequest(
            tenantId, projectCode, eventId, expectedPreviousItemId, eventType,
            new ProjectActorRefDto(actorType, actorId), agent, environment,
            "IN_PROGRESS", summary, null, null, null, null, null, null, null, null, model);

        return fixture.Client.PostAsJsonAsync("/api/project-control/events", request, JsonOptions);
    }

    [SkippableFact]
    public async Task GetHistory_MultipleEvents_ReturnsThemInChronologicalOrder()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "HISTORYORDER", "Historial ordenado");

        var first = await PostEventRawAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "HISTORYORDER", Guid.NewGuid(), null, "Primer evento",
            eventType: "CLAIM");
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var firstItemId = firstBody.GetProperty("itemId").GetInt64();

        await PostEventRawAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "HISTORYORDER", Guid.NewGuid(), firstItemId, "Segundo evento",
            agent: "CODEX", environment: "Codex CLI");

        var history = await fixture.Client.GetFromJsonAsync<ProjectHistoryDto>(
            $"/api/project-control/projects/HISTORYORDER/events?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);

        Assert.NotNull(history);
        Assert.Equal("HISTORYORDER", history!.ProjectCode);
        Assert.Equal(2, history.Interventions.Count);
        Assert.Equal("Primer evento", history.Interventions[0].Summary);
        Assert.Equal("CLAIM", history.Interventions[0].EventType);
        Assert.Equal("Segundo evento", history.Interventions[1].Summary);
        Assert.Equal("CODEX", history.Interventions[1].Agent);
        Assert.Equal("Codex CLI", history.Interventions[1].Environment);
        Assert.True(history.Interventions[0].ItemId < history.Interventions[1].ItemId);
    }

    [SkippableFact]
    public async Task GetHistory_UnknownProject_ReturnsNotFound()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();

        var response = await fixture.Client.GetAsync(
            $"/api/project-control/projects/NOEXISTE/events?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [SkippableFact]
    public async Task GetHistory_ProjectWithoutEvents_ReturnsEmptyList()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "HISTORYEMPTY", "Sin eventos todavia");

        var history = await fixture.Client.GetFromJsonAsync<ProjectHistoryDto>(
            $"/api/project-control/projects/HISTORYEMPTY/events?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);

        Assert.NotNull(history);
        Assert.Empty(history!.Interventions);
    }
}
