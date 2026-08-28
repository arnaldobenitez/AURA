using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AURA.Application.ProjectControl;

namespace AURA.IntegrationTests.ProjectControl;

/// <summary>
/// Covers the EXEC/Etapa identity, approval/rework/completion rules, explicit control
/// resolution, and legacy-environment handling introduced to replace the text-matched
/// EXEC model. See rules/AURA.md history for the "Corrección integral del circuito" spec
/// these scenarios were written against. AURA_PROTOCOL v2.1 layers authentication and a
/// per-(project, workStream) lease on top - every mutating event but CLAIM/REQUEST now needs
/// the caller to present the lease it holds (see docs/aura/AURA_PROTOCOL_v2.1.md), so most
/// sequences below open with an explicit CLAIM and thread PostedEvent.LeaseId/LeaseVersion
/// forward exactly like the item-chain id was already threaded.
/// </summary>
[Collection(ProjectControlDatabaseCollection.Name)]
public sealed class ProjectControlExecWorkflowTests(ProjectControlDatabaseFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static void RequireDatabase(ProjectControlDatabaseFixture fixture)
    {
        Skip.IfNot(fixture.IsAvailable, "Requiere conectividad con el SQL Server de desarrollo (AURA_DESIGNTIME_CONNSTR o User Secrets de AURA.Api).");
    }

    private async Task<ProjectControlProjectDto> GetProjectAsync(string projectCode)
    {
        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);
        return Assert.Single(board!.Projects, p => p.ProjectCode == projectCode);
    }

    private async Task<PostedEvent> PostEventAsync(
        string projectCode, Guid eventId, PostedEvent? previous, string summary,
        string actorType = "HUMAN", int actorId = 1, string? model = null,
        string? nextControlType = null, int? nextControlId = null,
        string? stage = null, decimal? stageProgress = null, int? totalStages = null,
        string eventType = "PROGRESS", string? agent = null, string? environment = null, string? details = null)
    {
        var response = await PostEventRawAsync(
            projectCode, eventId, previous?.ItemId, summary, actorType, actorId, model,
            nextControlType, nextControlId, stage, stageProgress, totalStages, eventType, agent, environment, details,
            previous?.LeaseId, previous?.LeaseVersion);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var itemId = body.GetProperty("itemId").GetInt64();

        Guid? leaseId = null;
        long? leaseVersion = null;
        if (body.TryGetProperty("lease", out var lease) && lease.ValueKind != JsonValueKind.Null)
        {
            leaseId = lease.GetProperty("leaseId").GetGuid();
            leaseVersion = lease.GetProperty("version").GetInt64();
        }

        return new PostedEvent(itemId, leaseId, leaseVersion);
    }

    private async Task<HttpResponseMessage> PostEventRawAsync(
        string projectCode, Guid eventId, long? expectedPreviousItemId, string summary,
        string actorType = "HUMAN", int actorId = 1, string? model = null,
        string? nextControlType = null, int? nextControlId = null,
        string? stage = null, decimal? stageProgress = null, int? totalStages = null,
        string eventType = "PROGRESS", string? agent = null, string? environment = null, string? details = null,
        Guid? leaseId = null, long? expectedLeaseVersion = null)
    {
        var request = new RegisterProjectEventRequest(
            ProjectControlDatabaseFixture.PrimaryTenantId,
            projectCode,
            eventId,
            expectedPreviousItemId,
            eventType,
            new ProjectActorRefDto(actorType, actorId),
            agent,
            environment,
            "IN_PROGRESS",
            summary,
            details,
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
        fixture.AddBearer(httpRequest, ProjectControlDatabaseFixture.PrimaryTenantId, actorType);
        return await fixture.Client.SendAsync(httpRequest);
    }

    [SkippableFact]
    public async Task RegisterEvent_CodexAnalystGpt5ChatGptWork_ReturnsFullIdentity()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "CODEXANALYST", "Codex analista");

        await PostEventAsync(
            "CODEXANALYST", Guid.NewGuid(), null, "Analisis con Codex",
            actorType: "AI_ANALYST", actorId: 1, model: "GPT-5",
            agent: "CODEX", environment: "ChatGPT Work", eventType: "CLAIM");

        var project = await GetProjectAsync("CODEXANALYST");
        Assert.Equal("AI_ANALYST", project.LastActorType);
        Assert.Equal("CODEX", project.LastAgent);
        Assert.Equal("GPT-5", project.LastModel);
        Assert.Equal("ChatGPT Work", project.LastEnvironment);
    }

    [SkippableFact]
    public async Task RegisterEvent_ClaudeCodeCoderSonnetVsCode_ReturnsFullIdentity()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "CLAUDECODER", "Claude Code codificador");

        await PostEventAsync(
            "CLAUDECODER", Guid.NewGuid(), null, "Codificando con Claude Code",
            actorType: "CODER", actorId: 1, model: "Sonnet",
            agent: "CLAUDE_CODE", environment: "VS Code", eventType: "CLAIM");

        var project = await GetProjectAsync("CLAUDECODER");
        Assert.Equal("CODER", project.LastActorType);
        Assert.Equal("CLAUDE_CODE", project.LastAgent);
        Assert.Equal("Sonnet", project.LastModel);
        Assert.Equal("VS Code", project.LastEnvironment);
    }

    [SkippableFact]
    public async Task RegisterEvent_CodexCoderDirectViaCodexCli_ReturnsFullIdentity()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "CODEXCODER", "Codex codificador directo");

        await PostEventAsync(
            "CODEXCODER", Guid.NewGuid(), null, "Codificando directo con Codex CLI",
            actorType: "CODER", actorId: 1, model: "GPT-5.1-Codex",
            agent: "CODEX", environment: "Codex CLI", eventType: "CLAIM");

        var project = await GetProjectAsync("CODEXCODER");
        Assert.Equal("CODER", project.LastActorType);
        Assert.Equal("CODEX", project.LastAgent);
        Assert.Equal("Codex CLI", project.LastEnvironment);
    }

    [SkippableFact]
    public async Task GetBoard_UnknownHistoricalEnvironment_ReturnsNullNotFabricated()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        var projectId = await fixture.CreateProjectAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "UNKNOWNENV", "Entorno desconocido");
        await fixture.CreateCodificadorAsync(ProjectControlDatabaseFixture.PrimaryTenantId, 711, "Pedro Rodriguez");

        await fixture.CreateLegacyItemWithoutModelAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, projectId, actorType: 3, coderId: 711,
            summary: "Evento historico sin agente/entorno propios");

        var project = await GetProjectAsync("UNKNOWNENV");
        Assert.Equal("CODER", project.LastActorType);
        Assert.Equal("Pedro Rodriguez", project.LastActorName);
        // Sin dato explicito ni coincidencia con el catalogo legacy conocido: nunca se fabrica.
        Assert.Null(project.LastEnvironment);
    }

    [SkippableFact]
    public async Task GetBoard_LegacyVsCodeCatalogName_ResolvesAsEnvironmentNotPerson()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        var projectId = await fixture.CreateProjectAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, "LEGACYVSCODE", "Legado Vs Code");
        await fixture.CreateCodificadorAsync(ProjectControlDatabaseFixture.PrimaryTenantId, 712, "Vs Code");

        await fixture.CreateLegacyItemWithoutModelAsync(
            ProjectControlDatabaseFixture.PrimaryTenantId, projectId, actorType: 3, coderId: 712,
            summary: "Evento historico registrado bajo el codificador 'Vs Code'");

        var project = await GetProjectAsync("LEGACYVSCODE");
        Assert.Equal("CODER", project.LastActorType);
        // "Vs Code" nunca debe mostrarse como si fuera una persona.
        Assert.NotEqual("Vs Code", project.LastActorName);
        Assert.Equal("VS Code", project.LastEnvironment);
    }

    [SkippableFact]
    public async Task GetBoard_ExplicitIdentityOverLegacyVsCodeCatalogId_NeverShowsCatalogNameAsPerson()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "VSCODEPLUSAGENT", "Vs Code con identidad explicita");
        // Reproduce el dato real: un codificador de catalogo llamado literalmente "Vs Code"
        // (entorno legacy, no persona) usado en un evento que SI trae agente/modelo/entorno
        // propios. El nombre de catalogo nunca debe mostrarse como si fuera una persona,
        // tenga o no la fila ademas su propia identidad explicita.
        await fixture.CreateCodificadorAsync(ProjectControlDatabaseFixture.PrimaryTenantId, 713, "Vs Code");

        await PostEventAsync(
            "VSCODEPLUSAGENT", Guid.NewGuid(), null, "Claude Code toma el control",
            actorType: "CODER", actorId: 713, model: "Sonnet", agent: "CLAUDE_CODE", environment: "VS Code",
            eventType: "CLAIM");

        var project = await GetProjectAsync("VSCODEPLUSAGENT");
        Assert.NotEqual("Vs Code", project.LastActorName);
        Assert.NotEqual("Vs Code", project.CurrentControlName);
        Assert.Equal("CLAUDE_CODE", project.CurrentControlAgent);
        Assert.Equal("VS Code", project.CurrentControlEnvironment);
    }

    [SkippableFact]
    public async Task RegisterEvent_ClaimWithSimultaneousNextControl_IgnoresNextControlKeepsSelfIdentity()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "CLAIMSELF", "Claim con nextControl simultaneo");
        await fixture.CreateUsuarioAsync(ProjectControlDatabaseFixture.PrimaryTenantId, 720, "Arnaldo Benitez");

        await PostEventAsync(
            "CLAIMSELF", Guid.NewGuid(), null, "Claude Code toma el control",
            actorType: "CODER", actorId: 1, model: "Sonnet", agent: "CLAUDE_CODE", environment: "VS Code",
            eventType: "CLAIM",
            // Un nextControl simultaneo con CLAIM debe ser ignorado: quien toma el control es quien reclama.
            nextControlType: "HUMAN", nextControlId: 720);

        var project = await GetProjectAsync("CLAIMSELF");
        Assert.Equal("CODER", project.CurrentControlType);
        Assert.Equal("CLAUDE_CODE", project.CurrentControlAgent);
        Assert.Equal("Sonnet", project.CurrentControlModel);
        Assert.Equal("VS Code", project.CurrentControlEnvironment);
    }

    [SkippableFact]
    public async Task RegisterEvent_Handoff_ReturnsControlToHuman()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "HANDOFFCTRL", "Handoff de control");
        await fixture.CreateUsuarioAsync(ProjectControlDatabaseFixture.PrimaryTenantId, 721, "Arnaldo Benitez");

        var claim = await PostEventAsync(
            "HANDOFFCTRL", Guid.NewGuid(), null, "Codex toma el control",
            actorType: "CODER", actorId: 1, model: "Sonnet", agent: "CLAUDE_CODE", environment: "VS Code",
            eventType: "CLAIM");

        await PostEventAsync(
            "HANDOFFCTRL", Guid.NewGuid(), claim, "Devuelve el control a Arnaldo",
            actorType: "CODER", actorId: 1, model: "Sonnet", agent: "CLAUDE_CODE", environment: "VS Code",
            eventType: "HANDOFF", nextControlType: "HUMAN", nextControlId: 721);

        var project = await GetProjectAsync("HANDOFFCTRL");
        Assert.Equal("HUMAN", project.CurrentControlType);
        Assert.Equal("Arnaldo Benitez", project.CurrentControlName);
        Assert.Null(project.CurrentControlAgent);
        Assert.Null(project.CurrentControlModel);
        Assert.Null(project.CurrentControlEnvironment);
    }

    [SkippableFact]
    public async Task RegisterEvent_ApprovalBelowHundredPercent_ReturnsInvalidTransitionAndDoesNotClose()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "APPROVAL30", "Aprobacion al 30%");
        await fixture.CreateUsuarioAsync(ProjectControlDatabaseFixture.PrimaryTenantId, 730, "Arnaldo Benitez");

        var progress = await PostEventAsync(
            "APPROVAL30", Guid.NewGuid(), null, "Avance parcial de Etapa 1",
            actorType: "CODER", actorId: 1, model: "Sonnet", agent: "CLAUDE_CODE", environment: "VS Code",
            eventType: "CLAIM", stage: "Etapa 1", stageProgress: 30m, totalStages: 2);

        var approvalResponse = await PostEventRawAsync(
            "APPROVAL30", Guid.NewGuid(), progress.ItemId, "Intento de aprobar sin llegar al 100%",
            actorType: "HUMAN", actorId: 730, eventType: "APPROVAL", stage: "Etapa 1",
            leaseId: progress.LeaseId, expectedLeaseVersion: progress.LeaseVersion);

        Assert.Equal(HttpStatusCode.BadRequest, approvalResponse.StatusCode);
        var body = await approvalResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("VALIDATION_FAILED", body.GetProperty("status").GetString());

        var project = await GetProjectAsync("APPROVAL30");
        Assert.Equal(0, project.ClosedStages);
        Assert.Equal(0m, project.ExecProgress);
        Assert.Equal(30m, project.StageProgress);
    }

    [SkippableFact]
    public async Task RegisterEvent_Rework_ReducesExecProgress()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "REWORKEXEC", "Rework reduce el EXEC");
        await fixture.CreateUsuarioAsync(ProjectControlDatabaseFixture.PrimaryTenantId, 740, "Arnaldo Benitez");

        PostedEvent? previous = null;
        var isFirstOverall = true;
        foreach (var (stageName, isFirst) in new[] { ("Etapa 1", true), ("Etapa 2", false) })
        {
            previous = await PostEventAsync(
                "REWORKEXEC", Guid.NewGuid(), previous, $"Avance tecnico de {stageName}",
                actorType: "CODER", actorId: 1, model: "Sonnet", agent: "CLAUDE_CODE", environment: "VS Code",
                eventType: isFirstOverall ? "CLAIM" : "PROGRESS",
                stage: stageName, stageProgress: 100m, totalStages: isFirst ? 2 : null);
            isFirstOverall = false;
            previous = await PostEventAsync(
                "REWORKEXEC", Guid.NewGuid(), previous, $"Aprobacion de {stageName}",
                actorType: "HUMAN", actorId: 740, eventType: "APPROVAL", stage: stageName);
        }

        var afterApprovals = await GetProjectAsync("REWORKEXEC");
        Assert.Equal(2, afterApprovals.ClosedStages);
        Assert.Equal(100m, afterApprovals.ExecProgress);

        await PostEventAsync(
            "REWORKEXEC", Guid.NewGuid(), previous, "Se detecto un problema en Etapa 1, se reabre",
            actorType: "HUMAN", actorId: 740, eventType: "REWORK", stage: "Etapa 1");

        var afterRework = await GetProjectAsync("REWORKEXEC");
        Assert.Equal(1, afterRework.ClosedStages);
        Assert.Equal(50m, afterRework.ExecProgress);
    }

    [SkippableFact]
    public async Task RegisterEvent_CompletionWithUnapprovedStages_ReturnsInvalidTransition()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "COMPLETIONFAIL", "Completion sin todas aprobadas");
        await fixture.CreateUsuarioAsync(ProjectControlDatabaseFixture.PrimaryTenantId, 750, "Arnaldo Benitez");

        var progress = await PostEventAsync(
            "COMPLETIONFAIL", Guid.NewGuid(), null, "Avance de Etapa 1",
            actorType: "CODER", actorId: 1, model: "Sonnet", agent: "CLAUDE_CODE", environment: "VS Code",
            eventType: "CLAIM", stage: "Etapa 1", stageProgress: 100m, totalStages: 2);
        var approval = await PostEventAsync(
            "COMPLETIONFAIL", Guid.NewGuid(), progress, "Aprobacion de Etapa 1",
            actorType: "HUMAN", actorId: 750, eventType: "APPROVAL", stage: "Etapa 1");

        var completionResponse = await PostEventRawAsync(
            "COMPLETIONFAIL", Guid.NewGuid(), approval.ItemId, "Intento de cerrar sin aprobar Etapa 2",
            actorType: "HUMAN", actorId: 750, eventType: "COMPLETION",
            leaseId: approval.LeaseId, expectedLeaseVersion: approval.LeaseVersion);

        Assert.Equal(HttpStatusCode.BadRequest, completionResponse.StatusCode);
        var body = await completionResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("VALIDATION_FAILED", body.GetProperty("status").GetString());
    }

    [SkippableFact]
    public async Task RegisterEvent_Completion_KeepsExecAtHundredPercent()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "COMPLETIONOK", "Completion mantiene 100%");
        await fixture.CreateUsuarioAsync(ProjectControlDatabaseFixture.PrimaryTenantId, 760, "Arnaldo Benitez");

        PostedEvent? previous = null;
        var isFirstOverall = true;
        foreach (var (stageName, isFirst) in new[] { ("Etapa 1", true), ("Etapa 2", false) })
        {
            previous = await PostEventAsync(
                "COMPLETIONOK", Guid.NewGuid(), previous, $"Avance tecnico de {stageName}",
                actorType: "CODER", actorId: 1, model: "Sonnet", agent: "CLAUDE_CODE", environment: "VS Code",
                eventType: isFirstOverall ? "CLAIM" : "PROGRESS",
                stage: stageName, stageProgress: 100m, totalStages: isFirst ? 2 : null);
            isFirstOverall = false;
            previous = await PostEventAsync(
                "COMPLETIONOK", Guid.NewGuid(), previous, $"Aprobacion de {stageName}",
                actorType: "HUMAN", actorId: 760, eventType: "APPROVAL", stage: stageName);
        }

        await PostEventAsync(
            "COMPLETIONOK", Guid.NewGuid(), previous, "Se cierra el EXEC",
            actorType: "HUMAN", actorId: 760, eventType: "COMPLETION");

        var project = await GetProjectAsync("COMPLETIONOK");
        Assert.Equal(2, project.ClosedStages);
        Assert.Equal(100m, project.ExecProgress);
    }

    [SkippableFact]
    public async Task RegisterEvent_TwoConsecutiveExecs_DoNotMixStages()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "TWOEXECS", "Dos EXEC consecutivos");
        await fixture.CreateUsuarioAsync(ProjectControlDatabaseFixture.PrimaryTenantId, 770, "Arnaldo Benitez");

        var progress = await PostEventAsync(
            "TWOEXECS", Guid.NewGuid(), null, "Avance de Etapa 1 del primer EXEC",
            actorType: "CODER", actorId: 1, model: "Sonnet", agent: "CLAUDE_CODE", environment: "VS Code",
            eventType: "CLAIM", stage: "Etapa 1", stageProgress: 100m, totalStages: 1);
        var approval = await PostEventAsync(
            "TWOEXECS", Guid.NewGuid(), progress, "Aprobacion de Etapa 1 del primer EXEC",
            actorType: "HUMAN", actorId: 770, eventType: "APPROVAL", stage: "Etapa 1");
        var completion = await PostEventAsync(
            "TWOEXECS", Guid.NewGuid(), approval, "Se cierra el primer EXEC",
            actorType: "HUMAN", actorId: 770, eventType: "COMPLETION");

        var afterFirstExec = await GetProjectAsync("TWOEXECS");
        Assert.Equal(1, afterFirstExec.ExecId);
        Assert.Equal(100m, afterFirstExec.ExecProgress);

        // Nuevo REQUEST: abre un EXEC nuevo, aunque reutilice el mismo nombre de etapa.
        await PostEventAsync(
            "TWOEXECS", Guid.NewGuid(), completion, "Nuevo pedido de trabajo",
            actorType: "HUMAN", actorId: 770, eventType: "REQUEST",
            details: "Segunda ronda de trabajo", stage: "Etapa 1", stageProgress: 40m, totalStages: 3);

        var afterSecondExecRequest = await GetProjectAsync("TWOEXECS");
        Assert.Equal(2, afterSecondExecRequest.ExecId);
        Assert.Equal(3, afterSecondExecRequest.TotalStages);
        // La nueva etapa "Etapa 1" pertenece al EXEC 2: no hereda la aprobacion del EXEC 1.
        Assert.Equal(0, afterSecondExecRequest.ClosedStages);
        Assert.Equal(0m, afterSecondExecRequest.ExecProgress);
        Assert.Equal(40m, afterSecondExecRequest.StageProgress);
    }

    [SkippableFact]
    public async Task RegisterEvent_ExecDescriptionInheritedAcrossEvents()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "EXECDESC", "Descripcion de EXEC heredada");

        var first = await PostEventAsync(
            "EXECDESC", Guid.NewGuid(), null, "Arranca el EXEC",
            actorType: "HUMAN", actorId: 1, eventType: "REQUEST",
            details: "Migrar el modulo de facturacion a la nueva API");

        await PostEventAsync(
            "EXECDESC", Guid.NewGuid(), first, "Claude Code toma el control",
            actorType: "CODER", actorId: 1, model: "Sonnet", agent: "CLAUDE_CODE", environment: "VS Code",
            eventType: "CLAIM");

        var project = await GetProjectAsync("EXECDESC");
        Assert.Equal("Migrar el modulo de facturacion a la nueva API", project.ExecDescription);
    }

    [SkippableFact]
    public async Task RegisterEvent_FiveOfTenStagesApproved_ExecProgressIsExactlyFiftyPercent()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, "HALFEXEC", "Mitad de etapas");
        await fixture.CreateUsuarioAsync(ProjectControlDatabaseFixture.PrimaryTenantId, 780, "Arnaldo Benitez");

        PostedEvent? previous = null;
        for (var i = 1; i <= 5; i++)
        {
            var stageName = $"Etapa {i}";
            previous = await PostEventAsync(
                "HALFEXEC", Guid.NewGuid(), previous, $"Avance de {stageName}",
                actorType: "CODER", actorId: 1, model: "Sonnet", agent: "CLAUDE_CODE", environment: "VS Code",
                eventType: i == 1 ? "CLAIM" : "PROGRESS",
                stage: stageName, stageProgress: 100m, totalStages: i == 1 ? 10 : null);
            previous = await PostEventAsync(
                "HALFEXEC", Guid.NewGuid(), previous, $"Aprobacion de {stageName}",
                actorType: "HUMAN", actorId: 780, eventType: "APPROVAL", stage: stageName);
        }

        var project = await GetProjectAsync("HALFEXEC");
        Assert.Equal(10, project.TotalStages);
        Assert.Equal(5, project.ClosedStages);
        Assert.Equal(50.00m, project.ExecProgress);
    }
}
