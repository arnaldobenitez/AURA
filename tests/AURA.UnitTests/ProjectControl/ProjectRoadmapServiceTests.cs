using AURA.Application.AgentCredential;
using AURA.Application.ProjectControl;
using AURA.Domain.ProjectControl;

namespace AURA.UnitTests.ProjectControl;

public sealed class ProjectRoadmapServiceTests
{
    [Fact]
    public async Task RegisterEvent_RoadmapApprovalRejectsCoderBeforeRepository()
    {
        var repository = new RecordingProjectControlRepository();
        var service = new ProjectControlService(repository, new AgentLeaseOptions());
        var request = CreateApprovalRequest("CODER");

        var result = await service.RegisterEventAsync(
            request,
            new AuthenticatedActor(1, 7, ProjectActorType.Coder, null, "coder"),
            CancellationToken.None);

        Assert.Equal(RegisterProjectEventStatus.ValidationFailed, result.Status);
        Assert.Equal(0, repository.RegisterEventCalls);
    }

    [Fact]
    public async Task RegisterEvent_IngestionCredentialCannotInvokeGovernedExecution()
    {
        var repository = new RecordingProjectControlRepository();
        var service = new ProjectControlService(repository, new AgentLeaseOptions());
        var request = CreateApprovalRequest("INGESTION") with
        {
            EventType = "CLAIM",
            StageCode = null,
            StageOutcome = null,
        };

        var result = await service.RegisterEventAsync(
            request,
            new AuthenticatedActor(1, 42, ProjectActorType.Ingestion, null, "ingestion"),
            CancellationToken.None);

        Assert.Equal(RegisterProjectEventStatus.Forbidden, result.Status);
        Assert.Equal(0, repository.RegisterEventCalls);
    }

    [Fact]
    public async Task UpdateRoadmap_RequiresHumanAndUniqueCodes()
    {
        var repository = new RecordingProjectControlRepository();
        var service = new ProjectControlService(repository, new AgentLeaseOptions());
        var human = new AuthenticatedActor(1, 2, ProjectActorType.Human, 1, "human");
        var coder = new AuthenticatedActor(1, 7, ProjectActorType.Coder, null, "coder");
        var duplicatePlan = new UpdateProjectRoadmapRequest(
            1,
            null,
            Guid.NewGuid(),
            [
                new ProjectRoadmapStageDefinition("A-1", "Primera", 1, null, 9),
                new ProjectRoadmapStageDefinition("a-1", "Segunda", 2, null, 10),
            ]);

        var forbidden = await service.UpdateRoadmapAsync("AURA", duplicatePlan, coder, CancellationToken.None);
        var invalid = await service.UpdateRoadmapAsync("AURA", duplicatePlan, human, CancellationToken.None);

        Assert.Equal(UpdateProjectRoadmapStatus.Forbidden, forbidden.Status);
        Assert.Equal(UpdateProjectRoadmapStatus.InvalidPlan, invalid.Status);
        Assert.Equal(0, repository.UpdateRoadmapCalls);
    }

    private static RegisterProjectEventRequest CreateApprovalRequest(string actorType) => new(
        1,
        "AURA",
        Guid.NewGuid(),
        1,
        "APPROVAL",
        new ProjectActorRefDto(actorType, 1),
        "test-agent",
        "unit-test",
        "IN_PROGRESS",
        "Stage approval",
        null,
        "Stage 1",
        null,
        null,
        null,
        null,
        null,
        null,
        "test-model",
        null,
        null,
        null,
        "A-1",
        "PASS");

    private sealed class RecordingProjectControlRepository : IProjectControlRepository
    {
        public int RegisterEventCalls { get; private set; }
        public int UpdateRoadmapCalls { get; private set; }

        public Task<IReadOnlyList<ProjectControlProjectDto>> GetBoardAsync(int tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ProjectControlProjectDto>>([]);

        public Task<ProjectEventOutcome> RegisterEventAsync(ProjectEventCommand command, CancellationToken cancellationToken)
        {
            RegisterEventCalls++;
            return Task.FromResult(new ProjectEventOutcome(ProjectEventOutcomeStatus.Created, 2, null));
        }

        public Task<CreateProjectOutcome> CreateProjectAsync(CreateProjectCommand command, CancellationToken cancellationToken) =>
            Task.FromResult(new CreateProjectOutcome(CreateProjectOutcomeStatus.Created, 1));

        public Task<ProjectRoadmapDto?> GetRoadmapAsync(int tenantId, string projectCode, string? workStream, CancellationToken cancellationToken) =>
            Task.FromResult<ProjectRoadmapDto?>(null);

        public Task<UpdateProjectRoadmapResult> UpdateRoadmapAsync(string projectCode, UpdateProjectRoadmapRequest request, AuthenticatedActor actor, CancellationToken cancellationToken)
        {
            UpdateRoadmapCalls++;
            return Task.FromResult(new UpdateProjectRoadmapResult(UpdateProjectRoadmapStatus.Updated));
        }

        public Task<ProjectHistoryDto?> GetHistoryAsync(int tenantId, string projectCode, CancellationToken cancellationToken) =>
            Task.FromResult<ProjectHistoryDto?>(null);
    }
}
