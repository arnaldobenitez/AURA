using AURA.Application.AgentCredential;
using AURA.Application.Registration;
using AURA.Domain.ProjectControl;
using AURA.Domain.Registration;

namespace AURA.UnitTests.Registration;

public sealed class RegistrationEventServiceTests
{
    private static readonly AuthenticatedActor IngestionActor =
        new(1, 42, ProjectActorType.Ingestion, null, "turnomedix-ingestion");

    [Fact]
    public async Task RegisterAsync_ValidEvent_PersistsUnverifiedDeclaredIdentity()
    {
        var repository = new RecordingRegistrationEventRepository();
        var service = new RegistrationEventService(repository);
        var eventId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var request = CreateRequest(eventId) with
        {
            SessionId = sessionId,
            ClaimedActorType = "coder",
            ClaimedActorId = 7,
            ExternalProjectKey = "external-1",
            ProjectName = "TurnoMedix",
        };

        var result = await service.RegisterAsync(request, IngestionActor, CancellationToken.None);

        Assert.Equal(RegistrationEventStatus.Created, result.Status);
        Assert.Equal(eventId, result.EventId);
        Assert.Equal(10, result.ItemId);
        Assert.Equal("RESOLVED", result.ProjectResolution);
        var command = Assert.IsType<RegistrationEventCommand>(repository.LastCommand);
        Assert.Equal("AURA", command.ProjectCode);
        Assert.Equal("REGISTRATION_ONLY", command.Mode);
        Assert.Equal(RegistrationEventType.Progress, command.EventType);
        Assert.Equal("CODER", command.ClaimedActorType);
        Assert.Equal(7, command.ClaimedActorId);
        Assert.Equal("agent", command.AgentName);
        Assert.Equal(sessionId, command.SessionId);
        Assert.Equal(42, command.CredentialId);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEvent_ReturnsExistingItemIdempotently()
    {
        var repository = new RecordingRegistrationEventRepository
        {
            Outcome = new RegistrationEventOutcome(
                RegistrationEventStatus.AlreadyExists, 11, ProjectRegistrationResolution.Resolved),
        };
        var service = new RegistrationEventService(repository);
        var eventId = Guid.NewGuid();

        var result = await service.RegisterAsync(CreateRequest(eventId), IngestionActor, CancellationToken.None);

        Assert.Equal(RegistrationEventStatus.AlreadyExists, result.Status);
        Assert.Equal(eventId, result.EventId);
        Assert.Equal(11, result.ItemId);
        Assert.Equal("RESOLVED", result.ProjectResolution);
    }

    [Fact]
    public async Task RegisterAsync_NonIngestionCredential_IsForbiddenBeforeRepository()
    {
        var repository = new RecordingRegistrationEventRepository();
        var service = new RegistrationEventService(repository);
        var coder = new AuthenticatedActor(1, 7, ProjectActorType.Coder, null, "coder");

        var result = await service.RegisterAsync(CreateRequest(), coder, CancellationToken.None);

        Assert.Equal(RegistrationEventStatus.Forbidden, result.Status);
        Assert.Equal(0, repository.RegisterCalls);
    }

    [Fact]
    public async Task RegisterAsync_TenantMismatch_IsForbiddenBeforeRepository()
    {
        var repository = new RecordingRegistrationEventRepository();
        var service = new RegistrationEventService(repository);
        var request = CreateRequest() with { TenantId = 2 };

        var result = await service.RegisterAsync(request, IngestionActor, CancellationToken.None);

        Assert.Equal(RegistrationEventStatus.Forbidden, result.Status);
        Assert.Equal(0, repository.RegisterCalls);
    }

    [Theory]
    [InlineData("INFO", RegistrationEventType.Info)]
    [InlineData("PROGRESS", RegistrationEventType.Progress)]
    [InlineData("DECISION", RegistrationEventType.Decision)]
    [InlineData("BLOCKER", RegistrationEventType.Blocker)]
    [InlineData("HANDOFF", RegistrationEventType.Handoff)]
    [InlineData("REQUEST", RegistrationEventType.Request)]
    [InlineData("NOTE", RegistrationEventType.Note)]
    [InlineData("ERROR", RegistrationEventType.Error)]
    public async Task RegisterAsync_AllowedEventTypes_AreAccepted(string eventType, RegistrationEventType expected)
    {
        var repository = new RecordingRegistrationEventRepository();
        var service = new RegistrationEventService(repository);

        var result = await service.RegisterAsync(
            CreateRequest() with { EventType = eventType },
            IngestionActor,
            CancellationToken.None);

        Assert.Equal(RegistrationEventStatus.Created, result.Status);
        Assert.Equal(expected, repository.LastCommand!.EventType);
    }

    [Theory]
    [InlineData("CLAIM")]
    [InlineData("APPROVAL")]
    [InlineData("COMPLETION")]
    public async Task RegisterAsync_GovernedOrUnknownEventTypes_AreRejected(string eventType)
    {
        var repository = new RecordingRegistrationEventRepository();
        var service = new RegistrationEventService(repository);

        var result = await service.RegisterAsync(
            CreateRequest() with { EventType = eventType },
            IngestionActor,
            CancellationToken.None);

        Assert.Equal(RegistrationEventStatus.ValidationFailed, result.Status);
        Assert.Equal(0, repository.RegisterCalls);
    }

    [Theory]
    [InlineData("Password = abc123")]
    [InlineData("api_key: abc123")]
    [InlineData("Data Source=server;Initial Catalog=db")]
    [InlineData("Bearer abcdefghijklmnopqrstuvwxyz")]
    [InlineData("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.abc.def")]
    [InlineData("<token>abc</token>")]
    [InlineData("-----BEGIN PRIVATE KEY-----")]
    public async Task RegisterAsync_SecretMaterial_IsRejectedBeforeRepository(string secret)
    {
        var repository = new RecordingRegistrationEventRepository();
        var service = new RegistrationEventService(repository);
        var request = CreateRequest() with { Details = $"Detalle inseguro: {secret}" };

        var result = await service.RegisterAsync(request, IngestionActor, CancellationToken.None);

        Assert.Equal(RegistrationEventStatus.ValidationFailed, result.Status);
        Assert.Contains(result.ValidationErrors!, error => error.Contains("material sensible", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, repository.RegisterCalls);
    }

    [Fact]
    public async Task RegisterAsync_ClaimedGovernedIdentity_DoesNotGrantAuthority()
    {
        var repository = new RecordingRegistrationEventRepository();
        var service = new RegistrationEventService(repository);

        var result = await service.RegisterAsync(
            CreateRequest() with { ClaimedActorType = "HUMAN", ClaimedActorId = 1 },
            IngestionActor,
            CancellationToken.None);

        Assert.Equal(RegistrationEventStatus.Created, result.Status);
        Assert.Equal("HUMAN", repository.LastCommand!.ClaimedActorType);
        Assert.Equal(1, repository.LastCommand.ClaimedActorId);
    }

    [Fact]
    public async Task RegisterAsync_GovernedMode_IsRejectedOnRegistrationEndpoint()
    {
        var repository = new RecordingRegistrationEventRepository();
        var service = new RegistrationEventService(repository);

        var result = await service.RegisterAsync(
            CreateRequest() with { Mode = "GOVERNED_EXECUTION" },
            IngestionActor,
            CancellationToken.None);

        Assert.Equal(RegistrationEventStatus.ValidationFailed, result.Status);
        Assert.Equal(0, repository.RegisterCalls);
    }

    [Fact]
    public async Task RegisterAsync_VerifiedIdentityClaim_IsRejected()
    {
        var repository = new RecordingRegistrationEventRepository();
        var service = new RegistrationEventService(repository);

        var result = await service.RegisterAsync(
            CreateRequest() with { IdentityStatus = "VERIFIED" },
            IngestionActor,
            CancellationToken.None);

        Assert.Equal(RegistrationEventStatus.ValidationFailed, result.Status);
        Assert.Equal(0, repository.RegisterCalls);
    }

    private static RegisterRegistrationEventRequest CreateRequest(Guid? eventId = null) => new(
        eventId ?? Guid.NewGuid(),
        1,
        "aura",
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
        new DateTimeOffset(2026, 9, 27, 12, 0, 0, TimeSpan.Zero),
        null,
        null,
        "UNVERIFIED");

    [Fact]
    public async Task Register_ProjectNotAssigned_IsForbiddenWithoutPersisting()
    {
        var repository = new RecordingRegistrationEventRepository { HasProjectScope = false };
        var service = new RegistrationEventService(repository);

        var result = await service.RegisterAsync(CreateRequest(), IngestionActor, CancellationToken.None);

        Assert.Equal(RegistrationEventStatus.Forbidden, result.Status);
        Assert.Equal(0, repository.RegisterCalls);
    }

    private sealed class RecordingRegistrationEventRepository : IRegistrationEventRepository
    {
        public int RegisterCalls { get; private set; }
        public RegistrationEventCommand? LastCommand { get; private set; }
        public RegistrationEventOutcome Outcome { get; set; } = new(
            RegistrationEventStatus.Created,
            10,
            ProjectRegistrationResolution.Resolved);
        public bool HasProjectScope { get; set; } = true;

        public Task<RegistrationEventOutcome> RegisterAsync(
            RegistrationEventCommand command,
            CancellationToken cancellationToken)
        {
            RegisterCalls++;
            LastCommand = command;
            return Task.FromResult(Outcome);
        }

        public Task<RegistrationEventDto?> GetAsync(
            int tenantId,
            Guid eventId,
            CancellationToken cancellationToken) =>
            Task.FromResult<RegistrationEventDto?>(null);

        public Task<bool> CanCredentialRegisterForProjectAsync(
            int tenantId,
            int credentialId,
            string projectCode,
            CancellationToken cancellationToken) =>
            Task.FromResult(HasProjectScope);
    }
}
