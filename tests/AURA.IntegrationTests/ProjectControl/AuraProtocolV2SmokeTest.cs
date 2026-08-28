using System.Net.Http.Json;
using System.Text.Json;
using AURA.Application.ProjectControl;

namespace AURA.IntegrationTests.ProjectControl;

/// <summary>
/// End-to-end smoke test for AURA_PROTOCOL's lifecycle (AURA-PROTOCOL-V2-00 section 29,
/// extended by AURA-PROTOCOL-V2-01 with authentication + lease): ANALYST(HUMAN) opens work ->
/// CODING_AGENT authenticates, claims, progresses and hands back for review -> ANALYST(HUMAN)
/// approves and closes -> READ-BACK confirms AURA actually recorded each step. Uses a
/// controlled, disposable project under the same safe out-of-range tenant already used by
/// ProjectControlDatabaseFixture (900001/900002) - no historical or production data touched,
/// and the fixture wipes it before and after the whole test run.
/// </summary>
[Collection(ProjectControlDatabaseCollection.Name)]
public sealed class AuraProtocolV2SmokeTest(ProjectControlDatabaseFixture fixture)
{
    private const string ProjectCode = "AURAV2SMOKE";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static void RequireDatabase(ProjectControlDatabaseFixture fixture)
    {
        Skip.IfNot(fixture.IsAvailable, "Requiere conectividad con el SQL Server de desarrollo (AURA_DESIGNTIME_CONNSTR o User Secrets de AURA.Api).");
    }

    private async Task<ProjectControlProjectDto> ReadBackAsync()
    {
        var board = await fixture.Client.GetFromJsonAsync<ProjectControlBoardDto>(
            $"/api/project-control?tenantId={ProjectControlDatabaseFixture.PrimaryTenantId}", JsonOptions);
        return Assert.Single(board!.Projects, p => p.ProjectCode == ProjectCode);
    }

    private async Task<PostedEvent> RegisterAsync(
        Guid eventId, long? expectedPreviousItemId, string eventType, string actorType, int actorId,
        string summary, string? agent = null, string? model = null, string? environment = null,
        string? details = null, string? stage = null, decimal? stageProgress = null, int? totalStages = null,
        string? nextControlType = null, int? nextControlId = null,
        Guid? leaseId = null, long? expectedLeaseVersion = null)
    {
        var request = new RegisterProjectEventRequest(
            ProjectControlDatabaseFixture.PrimaryTenantId,
            ProjectCode,
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
        var response = await fixture.Client.SendAsync(httpRequest);
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

        return new PostedEvent(itemId, newLeaseId, newLeaseVersion);
    }

    [SkippableFact]
    public async Task FullLifecycle_AnalystToCoderToAnalyst_IsConfirmedByReadBackAtEveryStep()
    {
        RequireDatabase(fixture);
        await fixture.ResetTestDataAsync();
        await fixture.CreateProjectAsync(ProjectControlDatabaseFixture.PrimaryTenantId, ProjectCode, "Smoke test AURA_PROTOCOL");
        await fixture.CreateUsuarioAsync(ProjectControlDatabaseFixture.PrimaryTenantId, 800, "Analista de prueba");

        // 1. ANALYST (HUMAN-only, authenticated - AURA_PROTOCOL_v2.1 closes anonymous writes)
        //    opens a new EXEC. REQUEST is HUMAN governance, no lease involved.
        var request = await RegisterAsync(
            Guid.NewGuid(), null, "REQUEST", "HUMAN", 800,
            "Se solicita una mejora de prueba", details: "EXEC de humo para AURA_PROTOCOL",
            stage: "Etapa unica", stageProgress: 0m, totalStages: 1);

        var afterRequest = await ReadBackAsync();
        Assert.Equal("Se solicita una mejora de prueba", afterRequest.Summary);
        Assert.Equal("EXEC de humo para AURA_PROTOCOL", afterRequest.ExecDescription);
        Assert.Equal(1, afterRequest.TotalStages);

        // 2. CODING_AGENT authenticates and claims control (TAKE_CONTROL) - acquires the lease.
        var claim = await RegisterAsync(
            Guid.NewGuid(), request.ItemId, "CLAIM", "CODER", 1,
            "Claude Code toma el control", agent: "CLAUDE_CODE", model: "Sonnet 5", environment: "VS Code");
        Assert.NotNull(claim.LeaseId);

        var afterClaim = await ReadBackAsync();
        Assert.Equal("CODER", afterClaim.CurrentControlType);
        Assert.Equal("CLAUDE_CODE", afterClaim.CurrentControlAgent);

        // 3. CODING_AGENT progresses the stage to 100% - must present the lease it holds.
        var progress = await RegisterAsync(
            Guid.NewGuid(), claim.ItemId, "PROGRESS", "CODER", 1,
            "Implementacion completada", agent: "CLAUDE_CODE", model: "Sonnet 5", environment: "VS Code",
            stage: "Etapa unica", stageProgress: 100m,
            leaseId: claim.LeaseId, expectedLeaseVersion: claim.LeaseVersion);

        var afterProgress = await ReadBackAsync();
        Assert.Equal(100m, afterProgress.StageProgress);
        Assert.Equal(0, afterProgress.ClosedStages);

        // 4. CODING_AGENT hands control back to the human for review (cannot self-approve) -
        //    atomic reassignment of the same lease to the declared next control.
        var handoff = await RegisterAsync(
            Guid.NewGuid(), progress.ItemId, "HANDOFF", "CODER", 1,
            "Entrega para revision", agent: "CLAUDE_CODE", model: "Sonnet 5", environment: "VS Code",
            nextControlType: "HUMAN", nextControlId: 800,
            leaseId: progress.LeaseId, expectedLeaseVersion: progress.LeaseVersion);

        var afterHandoff = await ReadBackAsync();
        Assert.Equal("HUMAN", afterHandoff.CurrentControlType);
        Assert.Equal("Analista de prueba", afterHandoff.CurrentControlName);

        // 5. ANALYST (HUMAN-only) approves and closes the stage - governance action, no lease.
        var approval = await RegisterAsync(
            Guid.NewGuid(), handoff.ItemId, "APPROVAL", "HUMAN", 800,
            "Etapa aprobada", stage: "Etapa unica");

        var afterApproval = await ReadBackAsync();
        Assert.Equal(1, afterApproval.ClosedStages);
        Assert.Equal(100m, afterApproval.ExecProgress);

        // 6. ANALYST (HUMAN-only) closes the EXEC now that every stage is approved.
        await RegisterAsync(
            Guid.NewGuid(), approval.ItemId, "COMPLETION", "HUMAN", 800,
            "EXEC cerrado");

        var afterCompletion = await ReadBackAsync();
        Assert.Equal(100m, afterCompletion.ExecProgress);
        Assert.Equal(1, afterCompletion.ClosedStages);

        await fixture.ResetTestDataAsync();
    }
}
