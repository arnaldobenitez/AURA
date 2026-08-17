using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AURA.Application.ProjectControl;

namespace AURA.IntegrationTests.ProjectControl;

[CollectionDefinition(Name)]
public sealed class ProjectControlDatabaseCollection : ICollectionFixture<ProjectControlDatabaseFixture>
{
    public const string Name = "ProjectControlDatabase";
}

[Collection(ProjectControlDatabaseCollection.Name)]
public sealed class ProjectControlEndpointTests(ProjectControlDatabaseFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static void RequireDatabase(ProjectControlDatabaseFixture fixture)
    {
        Skip.IfNot(fixture.IsAvailable, "Requiere conectividad con el SQL Server de desarrollo (AURA_DESIGNTIME_CONNSTR o User Secrets de AURA.Api).");
    }

    [SkippableFact]
    public async Task GetBoard_ProjectWithoutItems_ReturnsRowWithNoActivity()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "NOACTIVITY", "Sin Actividad");

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);

        Assert.NotNull(board);
        var project = Assert.Single(board!.Projects);
        Assert.Equal("NOACTIVITY", project.ProjectCode);
        Assert.Null(project.LastItemId);
        Assert.Null(project.LastActivityUtc);
        Assert.Null(project.LastEventType);
        Assert.Null(project.LastActorType);
    }

    [SkippableFact]
    public async Task GetBoard_MapsProjectStatusValuesUsedByTheGrid()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();

        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "DRAFT", "Borrador", 0);
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "ACTIVE", "Activo", 1);
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "PAUSED", "Suspendido", 2);
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "BLOCKED", "Bloqueado", 3);
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "COMPLETED", "Cerrado", 4);

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);

        Assert.Equal("DRAFT", Assert.Single(board!.Projects, p => p.ProjectCode == "DRAFT").ProjectStatus);
        Assert.Equal("ACTIVE", Assert.Single(board.Projects, p => p.ProjectCode == "ACTIVE").ProjectStatus);
        Assert.Equal("PAUSED", Assert.Single(board.Projects, p => p.ProjectCode == "PAUSED").ProjectStatus);
        Assert.Equal("BLOCKED", Assert.Single(board.Projects, p => p.ProjectCode == "BLOCKED").ProjectStatus);
        Assert.Equal("COMPLETED", Assert.Single(board.Projects, p => p.ProjectCode == "COMPLETED").ProjectStatus);
    }

    [SkippableFact]
    public async Task GetBoard_MultipleEvents_ReturnsOnlyTheMostRecentOne()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "MULTIEVENT", "Multi Evento");

        long? previous = null;
        foreach (var summary in new[] { "Primer avance", "Segundo avance", "Tercer avance (el último)" })
        {
            var created = await PostEventAsync(
                ProjectControlDatabaseFixture.PrimaryTenantId, "MULTIEVENT", Guid.NewGuid(), previous, summary);
            previous = created;
        }

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);

        var project = Assert.Single(board!.Projects, p => p.ProjectCode == "MULTIEVENT");
        Assert.Equal("Tercer avance (el último)", project.Summary);
        Assert.Equal(3, project.LastItemId);
    }

    [SkippableFact]
    public async Task GetBoard_LatestItem_ReturnsExecAndStageProgress()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        var projectId = await fixture.CreateProjectAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "EXECSTAGE", "EXEC con etapas");

        var itemId = await PostEventAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId,
            "EXECSTAGE",
            Guid.NewGuid(),
            null,
            "Avance de etapa");

        await fixture.SetItemProgressAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId,
            projectId,
            itemId!.Value,
            "Incorporar seguimiento por etapas",
            "Actualizar la grilla de control",
            62.50m,
            25m);

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);

        var project = Assert.Single(board!.Projects, p => p.ProjectCode == "EXECSTAGE");
        Assert.Equal("Incorporar seguimiento por etapas", project.ExecDescription);
        Assert.Equal("Actualizar la grilla de control", project.CurrentStage);
        Assert.Equal(62.50m, project.StageProgress);
        Assert.Equal(25m, project.ExecProgress);
    }

    [SkippableFact]
    public async Task GetBoard_ActorWithoutCatalogEntry_UsesFallbackName()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "NOCATALOG", "Sin Catalogo");

        await PostEventAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "NOCATALOG", Guid.NewGuid(), null,
            "Evento con codificador inexistente", actorType: "CODER", actorId: 987654);

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);

        var project = Assert.Single(board!.Projects, p => p.ProjectCode == "NOCATALOG");
        Assert.Equal("CODER", project.LastActorType);
        Assert.Equal("Codificador #987654", project.LastActorName);
    }

    [SkippableFact]
    public async Task RegisterEvent_SameEventIdTwice_DoesNotDuplicateAndReturnsSameItem()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "IDEMPOTENT", "Idempotente");
        var eventId = Guid.NewGuid();

        var first = await PostEventRawAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "IDEMPOTENT", eventId, null, "Primer intento");
        var second = await PostEventRawAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "IDEMPOTENT", eventId, null, "Primer intento");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(firstBody.GetProperty("itemId").GetInt64(), secondBody.GetProperty("itemId").GetInt64());

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);
        var project = Assert.Single(board!.Projects, p => p.ProjectCode == "IDEMPOTENT");
        Assert.Equal(1, project.LastItemId);
    }

    [SkippableFact]
    public async Task RegisterEvent_WrongExpectedPreviousItemId_ReturnsConflictWithCurrentLastItemId()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "CONFLICT", "Conflicto");

        await PostEventAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "CONFLICT", Guid.NewGuid(), null, "Primer evento");

        var response = await PostEventRawAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "CONFLICT", Guid.NewGuid(), expectedPreviousItemId: 999, summary: "No debería aplicarse");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CONFLICT", body.GetProperty("status").GetString());
        Assert.Equal(1, body.GetProperty("currentLastItemId").GetInt64());
    }

    [SkippableFact]
    public async Task GetBoard_DifferentTenants_NeverSeeEachOthersProjects()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "TENANTSCOPE", "Alcance Tenant Primario");

        var boardForOtherTenant = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.SecondaryTenantId}", JsonOptions);

        Assert.NotNull(boardForOtherTenant);
        Assert.DoesNotContain(boardForOtherTenant!.Projects, p => p.ProjectCode == "TENANTSCOPE");

        // A project code lookup for the write path is tenant-scoped too: the same code
        // under a different tenant must not be found.
        var response = await PostEventRawAsync(
            ProjectControlDatabaseFixture.SecondaryTenantId, "TENANTSCOPE", Guid.NewGuid(), null, "No debería encontrar el proyecto");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<long?> PostEventAsync(
        int tenantId, string projectCode, Guid eventId, long? previousItemId, string summary,
        string actorType = "CODER", int actorId = 1, string? model = null,
        string? nextControlType = null, int? nextControlId = null)
    {
        var response = await PostEventRawAsync(
            tenantId, projectCode, eventId, previousItemId, summary, actorType, actorId, model, nextControlType, nextControlId);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("itemId").GetInt64();
    }

    private Task<HttpResponseMessage> PostEventRawAsync(
        int tenantId, string projectCode, Guid eventId, long? expectedPreviousItemId, string summary,
        string actorType = "CODER", int actorId = 1, string? model = null,
        string? nextControlType = null, int? nextControlId = null)
    {
        var request = new RegisterProjectEventRequest(
            tenantId,
            projectCode,
            eventId,
            expectedPreviousItemId,
            "PROGRESS",
            new ProjectActorRefDto(actorType, actorId),
            "IN_PROGRESS",
            summary,
            null,
            nextControlType is null ? null : new ProjectActorRefDto(nextControlType, nextControlId),
            null,
            null,
            null,
            model);

        return fixture.Client.PostAsJsonAsync("/api/project-control/events", request, JsonOptions);
    }

    [SkippableFact]
    public async Task RegisterEvent_CodexActorWithModel_ReturnsModelOnBoard()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "CODEXMODEL", "Codex Modelo");

        await PostEventAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "CODEXMODEL", Guid.NewGuid(), null,
            "Avance de Codex", actorType: "CODER", actorId: 501, model: "Sonnet 5");

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);

        var project = Assert.Single(board!.Projects, p => p.ProjectCode == "CODEXMODEL");
        Assert.Equal("CODER", project.LastActorType);
        Assert.Equal("Sonnet 5", project.LastModel);
    }

    [SkippableFact]
    public async Task RegisterEvent_GrokActorWithModel_ReturnsModelOnBoard()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "GROKMODEL", "Grok Modelo");

        await PostEventAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "GROKMODEL", Guid.NewGuid(), null,
            "Avance de Grok", actorType: "AI_ANALYST", actorId: 502, model: "Fable");

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);

        var project = Assert.Single(board!.Projects, p => p.ProjectCode == "GROKMODEL");
        Assert.Equal("AI_ANALYST", project.LastActorType);
        Assert.Equal("Fable", project.LastModel);
    }

    [SkippableFact]
    public async Task RegisterEvent_HumanActor_UsesNHumanAndHasNoModel()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "HUMANACTOR", "Actor Humano");
        await fixture.CreateUsuarioAsync(ProjectControlDatabaseFixture.PrimaryTenantId, 601, "Juan Pérez");

        await PostEventAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "HUMANACTOR", Guid.NewGuid(), null,
            "Avance manual", actorType: "HUMAN", actorId: 601, model: "Sonnet 5");

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);

        var project = Assert.Single(board!.Projects, p => p.ProjectCode == "HUMANACTOR");
        Assert.Equal("HUMAN", project.LastActorType);
        Assert.Equal("Juan Pérez", project.LastActorName);
        Assert.Null(project.LastModel);
    }

    [SkippableFact]
    public async Task GetBoard_HumanControlHandoff_ResolvesNameFromUsuarios()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "HANDOFFHUMAN", "Handoff a Humano");
        await fixture.CreateUsuarioAsync(ProjectControlDatabaseFixture.PrimaryTenantId, 602, "María Gómez");

        await PostEventAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "HANDOFFHUMAN", Guid.NewGuid(), null,
            "Handoff hacia humano", actorType: "CODER", actorId: 1, model: "Sonnet 5",
            nextControlType: "HUMAN", nextControlId: 602);

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);

        var project = Assert.Single(board!.Projects, p => p.ProjectCode == "HANDOFFHUMAN");
        Assert.Equal("HUMAN", project.CurrentControlType);
        Assert.Equal("María Gómez", project.CurrentControlName);
        // nHumano keeps tracing the real actor (CODER #1); it must not be overwritten with the next human's id.
        Assert.Equal("CODER", project.LastActorType);
    }

    [SkippableFact]
    public async Task GetBoard_HumanControlWithoutCatalogEntry_UsesFallbackName()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "HANDOFFNOCAT", "Handoff Sin Catalogo");

        await PostEventAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "HANDOFFNOCAT", Guid.NewGuid(), null,
            "Handoff hacia humano inexistente", actorType: "CODER", actorId: 1,
            nextControlType: "HUMAN", nextControlId: 987654);

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);

        var project = Assert.Single(board!.Projects, p => p.ProjectCode == "HANDOFFNOCAT");
        Assert.Equal("HUMAN", project.CurrentControlType);
        Assert.Equal("Humano #987654", project.CurrentControlName);
    }

    [SkippableFact]
    public async Task GetBoard_HistoricalItemWithoutModel_ReturnsNullModelAndDoesNotFail()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        var projectId = await fixture.CreateProjectAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "LEGACYNOMODEL", "Legado Sin Modelo");

        await fixture.CreateLegacyItemWithoutModelAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, projectId, actorType: 3, coderId: 701,
            summary: "Evento histórico previo a sModelo");

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);

        var project = Assert.Single(board!.Projects, p => p.ProjectCode == "LEGACYNOMODEL");
        Assert.Equal("CODER", project.LastActorType);
        Assert.Null(project.LastModel);
    }
}
