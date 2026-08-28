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

/// <summary>
/// Lease returned by a successful write (all mutating events except REQUEST/COMPLETION renew
/// or (re)assign one) - threaded into the next call in the same chain exactly like ItemId
/// already was, per AURA_PROTOCOL v2.1's optimistic-concurrency lease. See
/// docs/aura/AURA_PROTOCOL_v2.1.md.
/// </summary>
public readonly record struct PostedEvent(long ItemId, Guid? LeaseId, long? LeaseVersion);

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

        // El primero es CLAIM (adquiere el lease); el resto son PROGRESS reutilizandolo - la
        // semantica de "cual evento gano" no depende del tipo, asi que esto no cambia lo que la
        // prueba verifica.
        var summaries = new[] { "Primer avance", "Segundo avance", "Tercer avance (el último)" };
        PostedEvent? previous = null;
        for (var i = 0; i < summaries.Length; i++)
        {
            previous = await PostEventAsync(
                ProjectControlDatabaseFixture.PrimaryTenantId, "MULTIEVENT", Guid.NewGuid(),
                previous?.ItemId, summaries[i],
                eventType: i == 0 ? "CLAIM" : "PROGRESS",
                leaseId: previous?.LeaseId, expectedLeaseVersion: previous?.LeaseVersion);
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

        var posted = await PostEventAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId,
            "EXECSTAGE",
            Guid.NewGuid(),
            null,
            "Avance de etapa",
            eventType: "CLAIM");

        await fixture.SetItemProgressAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId,
            projectId,
            posted.ItemId,
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
        // El EXEC creado por el evento inicial no tiene nTotalEtapas (no se informo totalStages).
        Assert.Null(project.TotalStages);
        // nPorcEXEC es una fotografia grabada en el evento (hallazgo 11): se lee tal cual,
        // incluso cuando fue seteado directo por SQL bypaseando la API.
        Assert.Equal(25m, project.ExecProgress);
    }

    [SkippableFact]
    public async Task RegisterEvent_WithStageAndProgress_PersistsThemViaTheApi()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "EXECWRITE", "EXEC escribible");

        await PostEventAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId,
            "EXECWRITE",
            Guid.NewGuid(),
            null,
            "Avance con etapa",
            eventType: "CLAIM",
            stage: "Etapa 2 de 5",
            stageProgress: 40m,
            totalStages: 5);

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);

        var project = Assert.Single(board!.Projects, p => p.ProjectCode == "EXECWRITE");
        Assert.Equal("Etapa 2 de 5", project.CurrentStage);
        Assert.Equal(40m, project.StageProgress);
        Assert.Equal(5, project.TotalStages);
        // Nadie aprobo (APPROVAL) ninguna etapa todavia, asi que el EXEC sigue en 0%, no null.
        Assert.Equal(0, project.ClosedStages);
        Assert.Equal(0m, project.ExecProgress);
    }

    [SkippableFact]
    public async Task RegisterEvent_FirstEventWithoutStageOrProgress_StaysNull()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "NOEXEC", "Sin EXEC informado");

        var response = await PostEventRawAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "NOEXEC", Guid.NewGuid(), null, "Avance sin etapa",
            eventType: "CLAIM");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);

        var project = Assert.Single(board!.Projects, p => p.ProjectCode == "NOEXEC");
        Assert.Null(project.CurrentStage);
        Assert.Null(project.StageProgress);
        Assert.Null(project.TotalStages);
        Assert.Null(project.ExecProgress);
    }

    [SkippableFact]
    public async Task RegisterEvent_SubsequentEventWithoutStage_InheritsPreviousStageAndProgress()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "INHERITEXEC", "Hereda EXEC vigente");

        var first = await PostEventAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "INHERITEXEC", Guid.NewGuid(), null,
            "Primer avance", eventType: "CLAIM", stage: "Etapa 1", stageProgress: 30m, totalStages: 4);

        // El segundo evento no manda stage/stageProgress/totalStages: no deben perderse.
        await PostEventAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "INHERITEXEC", Guid.NewGuid(), first.ItemId,
            "Segundo avance, sin repetir la etapa", leaseId: first.LeaseId, expectedLeaseVersion: first.LeaseVersion);

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);

        var project = Assert.Single(board!.Projects, p => p.ProjectCode == "INHERITEXEC");
        Assert.Equal("Segundo avance, sin repetir la etapa", project.Summary);
        Assert.Equal("Etapa 1", project.CurrentStage);
        Assert.Equal(30m, project.StageProgress);
        Assert.Equal(4, project.TotalStages);
    }

    [SkippableFact]
    public async Task RegisterEvent_ProgressOutOfRange_ReturnsValidationFailed()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "OUTOFRANGE", "Fuera de rango");

        var response = await PostEventRawAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "OUTOFRANGE", Guid.NewGuid(), null, "Avance invalido",
            eventType: "CLAIM", stageProgress: 150m);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("VALIDATION_FAILED", body.GetProperty("status").GetString());
    }

    [SkippableFact]
    public async Task RegisterEvent_ProgressWithMoreThanTwoDecimals_ReturnsValidationFailed()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "TOOMANYDEC", "Demasiados decimales");

        var response = await PostEventRawAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "TOOMANYDEC", Guid.NewGuid(), null, "Avance invalido",
            eventType: "CLAIM", stageProgress: 33.333m);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task RegisterEvent_ApprovalByNonHumanActor_ReturnsValidationFailed()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "APPROVALCODER", "Aprobacion invalida");

        var claim = await PostEventAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "APPROVALCODER", Guid.NewGuid(), null,
            "Claude Code toma el control", actorType: "CODER", eventType: "CLAIM",
            agent: "CLAUDE_CODE", model: "Sonnet 5", environment: "VS Code");

        var response = await PostEventRawAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "APPROVALCODER", Guid.NewGuid(), claim.ItemId, "Intento de aprobar",
            actorType: "CODER", eventType: "APPROVAL", stage: "Etapa 1",
            agent: "CLAUDE_CODE", model: "Sonnet 5", environment: "VS Code",
            leaseId: claim.LeaseId, expectedLeaseVersion: claim.LeaseVersion);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("VALIDATION_FAILED", body.GetProperty("status").GetString());
    }

    [SkippableFact]
    public async Task RegisterEvent_RequestByNonHumanActor_ReturnsValidationFailed()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "REQUESTCODER", "Solicitud invalida");

        var response = await PostEventRawAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "REQUESTCODER", Guid.NewGuid(), null, "Intento de solicitar",
            actorType: "AI_ANALYST", eventType: "REQUEST",
            agent: "GROK", model: "Fable", environment: "Grok Web");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [SkippableFact]
    public async Task RegisterEvent_AgentAndEnvironment_PersistAndReadBackOnBoard()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "AGENTENV", "Agente y entorno");

        await PostEventAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "AGENTENV", Guid.NewGuid(), null,
            "Codificando con Claude Code", actorType: "CODER", actorId: 1, model: "Sonnet 5",
            agent: "CLAUDE_CODE", environment: "VS Code", eventType: "CLAIM");

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);

        var project = Assert.Single(board!.Projects, p => p.ProjectCode == "AGENTENV");
        Assert.Equal("CLAUDE_CODE", project.LastAgent);
        Assert.Equal("VS Code", project.LastEnvironment);
        Assert.Equal("Sonnet 5", project.LastModel);
    }

    [SkippableFact]
    public async Task RegisterEvent_FiveOfTenStagesApproved_ExecProgressIsFiftyPercent()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateUsuarioAsync(ProjectControlDatabaseFixture.PrimaryTenantId, 801, "Arnaldo Benitez");
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "TENSTAGES", "Diez etapas");

        PostedEvent? previous = null;
        for (var i = 1; i <= 5; i++)
        {
            var stageName = $"Etapa {i}";

            var progress = await PostEventAsync(
                ProjectControlDatabaseFixture.PrimaryTenantId, "TENSTAGES", Guid.NewGuid(), previous?.ItemId,
                $"Avance tecnico de {stageName}", stage: stageName, stageProgress: 100m,
                totalStages: i == 1 ? 10 : null,
                eventType: i == 1 ? "CLAIM" : "PROGRESS",
                leaseId: previous?.LeaseId, expectedLeaseVersion: previous?.LeaseVersion);
            previous = progress;

            var approval = await PostEventAsync(
                ProjectControlDatabaseFixture.PrimaryTenantId, "TENSTAGES", Guid.NewGuid(), previous?.ItemId,
                $"Aprobacion de {stageName}", actorType: "HUMAN", actorId: 801, eventType: "APPROVAL", stage: stageName,
                leaseId: previous?.LeaseId, expectedLeaseVersion: previous?.LeaseVersion);
            previous = approval;
        }

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);

        var project = Assert.Single(board!.Projects, p => p.ProjectCode == "TENSTAGES");
        Assert.Equal(10, project.TotalStages);
        Assert.Equal(5, project.ClosedStages);
        Assert.Equal(50.00m, project.ExecProgress);
    }

    [SkippableFact]
    public async Task GetBoard_NoExplicitHandoff_ControlFallsBackToCurrentActor()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "SELFCONTROL", "Control propio");

        await PostEventAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "SELFCONTROL", Guid.NewGuid(), null,
            "Claude Code toma el control", actorType: "CODER", actorId: 1, model: "Sonnet 5",
            agent: "CLAUDE_CODE", environment: "VS Code", eventType: "CLAIM");

        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);

        var project = Assert.Single(board!.Projects, p => p.ProjectCode == "SELFCONTROL");
        Assert.Equal("CODER", project.CurrentControlType);
        Assert.Equal("CLAUDE_CODE", project.CurrentControlAgent);
        Assert.Equal("Sonnet 5", project.CurrentControlModel);
        Assert.Equal("VS Code", project.CurrentControlEnvironment);
    }

    [SkippableFact]
    public async Task GetBoard_ActorWithoutCatalogEntry_UsesFallbackName()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "NOCATALOG", "Sin Catalogo");

        await PostEventAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "NOCATALOG", Guid.NewGuid(), null,
            "Evento con codificador inexistente", actorType: "CODER", actorId: 987654, model: "Sonnet 5",
            agent: "CODEX", environment: "Codex CLI", eventType: "CLAIM");

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

        var first = await PostEventRawAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "IDEMPOTENT", eventId, null, "Primer intento", eventType: "CLAIM");
        var second = await PostEventRawAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "IDEMPOTENT", eventId, null, "Primer intento", eventType: "CLAIM");

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

        await PostEventAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "CONFLICT", Guid.NewGuid(), null, "Primer evento", eventType: "CLAIM");

        var response = await PostEventRawAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "CONFLICT", Guid.NewGuid(), expectedPreviousItemId: 999, summary: "No debería aplicarse",
            eventType: "CLAIM");

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
            ProjectControlDatabaseFixture.SecondaryTenantId, "TENANTSCOPE", Guid.NewGuid(), null, "No debería encontrar el proyecto",
            eventType: "CLAIM");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<PostedEvent> PostEventAsync(
        int tenantId, string projectCode, Guid eventId, long? previousItemId, string summary,
        string actorType = "HUMAN", int actorId = 1, string? model = null,
        string? nextControlType = null, int? nextControlId = null,
        string? stage = null, decimal? stageProgress = null, int? totalStages = null,
        string eventType = "PROGRESS", string? agent = null, string? environment = null,
        Guid? leaseId = null, long? expectedLeaseVersion = null)
    {
        var response = await PostEventRawAsync(
            tenantId, projectCode, eventId, previousItemId, summary, actorType, actorId, model,
            nextControlType, nextControlId, stage, stageProgress, totalStages, eventType, agent, environment,
            leaseId, expectedLeaseVersion);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var itemId = body.GetProperty("itemId").GetInt64();

        Guid? newLeaseId = null;
        long? newLeaseVersion = null;
        if (body.TryGetProperty("lease", out var lease) && lease.ValueKind != JsonValueKind.Null)
        {
            newLeaseId = lease.GetProperty("leaseId").GetGuid();
            newLeaseVersion = lease.GetProperty("version").GetInt64();
        }

        return new PostedEvent(itemId, newLeaseId, newLeaseVersion);
    }

    private async Task<HttpResponseMessage> PostEventRawAsync(
        int tenantId, string projectCode, Guid eventId, long? expectedPreviousItemId, string summary,
        string actorType = "HUMAN", int actorId = 1, string? model = null,
        string? nextControlType = null, int? nextControlId = null,
        string? stage = null, decimal? stageProgress = null, int? totalStages = null,
        string eventType = "PROGRESS", string? agent = null, string? environment = null,
        Guid? leaseId = null, long? expectedLeaseVersion = null)
    {
        var request = new RegisterProjectEventRequest(
            tenantId,
            projectCode,
            eventId,
            expectedPreviousItemId,
            eventType,
            new ProjectActorRefDto(actorType, actorId),
            agent,
            environment,
            "IN_PROGRESS",
            summary,
            null,
            stage,
            stageProgress,
            totalStages,
            nextControlType is null ? null : new ProjectActorRefDto(nextControlType, nextControlId),
            null,
            null,
            null,
            model,
            null,
            leaseId,
            expectedLeaseVersion);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/project-control/events")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        fixture.AddBearer(httpRequest, tenantId, actorType);
        return await fixture.Client.SendAsync(httpRequest);
    }

    [SkippableFact]
    public async Task RegisterEvent_CodexActorWithModel_ReturnsModelOnBoard()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "CODEXMODEL", "Codex Modelo");

        await PostEventAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "CODEXMODEL", Guid.NewGuid(), null,
            "Avance de Codex", actorType: "CODER", actorId: 501, model: "Sonnet 5",
            agent: "CODEX", environment: "Codex CLI", eventType: "CLAIM");

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
            "Avance de Grok", actorType: "AI_ANALYST", actorId: 502, model: "Fable",
            agent: "GROK", environment: "Grok Web", eventType: "CLAIM");

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
            "Avance manual", actorType: "HUMAN", actorId: 601, model: "Sonnet 5", eventType: "CLAIM");

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

        var claim = await PostEventAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "HANDOFFHUMAN", Guid.NewGuid(), null,
            "Claude Code toma el control", actorType: "CODER", actorId: 1, model: "Sonnet 5",
            agent: "CLAUDE_CODE", environment: "VS Code", eventType: "CLAIM");

        await PostEventAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "HANDOFFHUMAN", Guid.NewGuid(), claim.ItemId,
            "Handoff hacia humano", actorType: "CODER", actorId: 1, model: "Sonnet 5",
            agent: "CLAUDE_CODE", environment: "VS Code",
            eventType: "HANDOFF", nextControlType: "HUMAN", nextControlId: 602,
            leaseId: claim.LeaseId, expectedLeaseVersion: claim.LeaseVersion);

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

        var claim = await PostEventAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "HANDOFFNOCAT", Guid.NewGuid(), null,
            "Claude Code toma el control", actorType: "CODER", actorId: 1, model: "Sonnet 5",
            agent: "CLAUDE_CODE", environment: "VS Code", eventType: "CLAIM");

        await PostEventAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "HANDOFFNOCAT", Guid.NewGuid(), claim.ItemId,
            "Handoff hacia humano inexistente", actorType: "CODER", actorId: 1, model: "Sonnet 5",
            agent: "CLAUDE_CODE", environment: "VS Code",
            eventType: "HANDOFF", nextControlType: "HUMAN", nextControlId: 987654,
            leaseId: claim.LeaseId, expectedLeaseVersion: claim.LeaseVersion);

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
