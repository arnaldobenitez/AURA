using AURA.Application.AgentCredential;
using AURA.Domain.ProjectControl;

namespace AURA.Application.ProjectControl;

public sealed class ProjectControlService(IProjectControlRepository repository, AgentLeaseOptions leaseOptions)
{
    private const int SummaryMaxLength = 300;
    private const int LongTextMaxLength = 1000;
    private const int ModelMaxLength = 100;
    private const int StageMaxLength = 100;
    private const int EnvironmentMaxLength = 100;
    private const int ProjectCodeMaxLength = 50;
    private const int StageCodeMaxLength = 50;
    private const int ProjectNameMaxLength = 100;
    private const int DescriptionMaxLength = 500;
    private const int RepositoryMaxLength = 2000;

    public Task<IReadOnlyList<ProjectControlProjectDto>> GetBoardAsync(int tenantId, CancellationToken cancellationToken)
    {
        return repository.GetBoardAsync(tenantId, cancellationToken);
    }

    public Task<ProjectHistoryDto?> GetHistoryAsync(int tenantId, string projectCode, CancellationToken cancellationToken)
    {
        return repository.GetHistoryAsync(tenantId, projectCode.Trim(), cancellationToken);
    }

    public Task<ProjectRoadmapDto?> GetRoadmapAsync(int tenantId, string projectCode, string? workStream, CancellationToken cancellationToken)
    {
        return repository.GetRoadmapAsync(tenantId, projectCode.Trim(), NullIfWhiteSpace(workStream), cancellationToken);
    }

    public async Task<UpdateProjectRoadmapResult> UpdateRoadmapAsync(
        string projectCode,
        UpdateProjectRoadmapRequest request,
        AuthenticatedActor authenticatedActor,
        CancellationToken cancellationToken)
    {
        request = request with { WorkStream = NullIfWhiteSpace(request.WorkStream) };
        if (authenticatedActor.ActorType != ProjectActorType.Human || authenticatedActor.TenantId != request.TenantId)
        {
            return new UpdateProjectRoadmapResult(UpdateProjectRoadmapStatus.Forbidden);
        }

        var errors = ValidateRoadmap(request);
        if (errors.Count > 0)
        {
            return new UpdateProjectRoadmapResult(UpdateProjectRoadmapStatus.InvalidPlan, errors);
        }

        return await repository.UpdateRoadmapAsync(projectCode.Trim(), request, authenticatedActor, cancellationToken);
    }

    private static List<string> ValidateRoadmap(UpdateProjectRoadmapRequest request)
    {
        var errors = new List<string>();
        if (request.TenantId <= 0)
        {
            errors.Add("tenantId debe ser mayor a cero.");
        }

        if (request.EventId == Guid.Empty)
        {
            errors.Add("eventId debe ser un GUID válido.");
        }

        if (request.Stages is null || request.Stages.Count == 0 || request.Stages.Count > 200)
        {
            errors.Add("El Roadmap debe contener entre 1 y 200 etapas.");
            return errors;
        }

        if (request.WorkStream is { Length: > StageMaxLength })
        {
            errors.Add($"workStream no puede superar {StageMaxLength} caracteres.");
        }

        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var orders = new HashSet<int>();
        foreach (var stage in request.Stages)
        {
            if (string.IsNullOrWhiteSpace(stage.StageCode) || stage.StageCode.Trim().Length > StageCodeMaxLength)
            {
                errors.Add($"stageCode es obligatorio y no puede superar {StageCodeMaxLength} caracteres.");
            }
            else if (!codes.Add(stage.StageCode.Trim()))
            {
                errors.Add($"stageCode duplicado: {stage.StageCode}.");
            }

            if (string.IsNullOrWhiteSpace(stage.Description) || stage.Description.Trim().Length > StageMaxLength)
            {
                errors.Add($"La descripción de cada etapa es obligatoria y no puede superar {StageMaxLength} caracteres.");
            }

            if (stage.Order <= 0 || !orders.Add(stage.Order))
            {
                errors.Add("El orden de las etapas debe ser único y mayor a cero.");
            }

            if (stage.AcceptanceCriteria is { Length: > 1000 })
            {
                errors.Add("Los criterios de aceptación no pueden superar 1000 caracteres.");
            }

            if (stage.ValidatorCredentialId <= 0)
            {
                errors.Add("Cada etapa debe tener un validatorCredentialId AI_ANALYST válido.");
            }
        }

        if (orders.Count == request.Stages.Count && !orders.Order().SequenceEqual(Enumerable.Range(1, orders.Count)))
        {
            errors.Add("El orden de las etapas debe ser consecutivo desde 1.");
        }

        return errors;
    }

    public async Task<RegisterProjectEventResult> RegisterEventAsync(
        RegisterProjectEventRequest request,
        AuthenticatedActor authenticatedActor,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request, out var eventType, out var status, out var actorType, out var nextControlType);

        if (errors.Count > 0)
        {
            return new RegisterProjectEventResult(RegisterProjectEventStatus.ValidationFailed, null, null, errors);
        }

        // Identity is authenticated, not asserted: reject anything the caller's own credential
        // does not back up, rather than trusting actor.type/actor.id from the body. See
        // AURA_PROTOCOL v2.2 authenticated identity and designated Roadmap approver rules.
        var forbiddenReason = CheckAuthorization(request, actorType, authenticatedActor);
        if (forbiddenReason is not null)
        {
            return new RegisterProjectEventResult(RegisterProjectEventStatus.Forbidden, null, null, [forbiddenReason]);
        }

        var command = new ProjectEventCommand(
            request.TenantId,
            request.ProjectCode.Trim(),
            request.EventId,
            request.ExpectedPreviousItemId,
            eventType,
            actorType,
            request.Actor.Id,
            NullIfWhiteSpace(request.Agent),
            NullIfWhiteSpace(request.Environment),
            status,
            request.Summary.Trim(),
            NullIfWhiteSpace(request.Details),
            NullIfWhiteSpace(request.Stage),
            request.StageProgress,
            request.TotalStages,
            nextControlType,
            request.NextControl?.Id,
            NullIfWhiteSpace(request.NextAction),
            NullIfWhiteSpace(request.Blocker),
            NullIfWhiteSpace(request.Reference),
            NullIfWhiteSpace(request.Model),
            NullIfWhiteSpace(request.WorkStream),
            request.LeaseId,
            request.ExpectedLeaseVersion,
            authenticatedActor.CredentialId,
            leaseOptions.Duration,
            NullIfWhiteSpace(request.StageCode),
            NullIfWhiteSpace(request.StageOutcome)?.ToUpperInvariant());

        var outcome = await repository.RegisterEventAsync(command, cancellationToken);

        return outcome.Status switch
        {
            ProjectEventOutcomeStatus.Created =>
                new RegisterProjectEventResult(RegisterProjectEventStatus.Created, outcome.ItemId, null, null, ToLeaseInfo(outcome.Lease)),
            ProjectEventOutcomeStatus.AlreadyExists =>
                new RegisterProjectEventResult(RegisterProjectEventStatus.AlreadyExists, outcome.ItemId, null, null, ToLeaseInfo(outcome.Lease)),
            ProjectEventOutcomeStatus.ProjectNotFound =>
                new RegisterProjectEventResult(RegisterProjectEventStatus.ProjectNotFound, null, null, null),
            ProjectEventOutcomeStatus.Conflict =>
                new RegisterProjectEventResult(RegisterProjectEventStatus.Conflict, null, outcome.CurrentLastItemId, null),
            ProjectEventOutcomeStatus.LeaseConflict =>
                new RegisterProjectEventResult(
                    RegisterProjectEventStatus.LeaseConflict, null, null,
                    [outcome.ErrorMessage ?? "Otro actor ya tiene el control de este proyecto/workstream."],
                    ToLeaseInfo(outcome.Lease)),
            ProjectEventOutcomeStatus.LeaseExpired =>
                new RegisterProjectEventResult(
                    RegisterProjectEventStatus.LeaseConflict,
                    null, null,
                    [outcome.ErrorMessage ?? "El lease expiró; tomá el control nuevamente con CLAIM."]),
            ProjectEventOutcomeStatus.InvalidTransition =>
                new RegisterProjectEventResult(
                    RegisterProjectEventStatus.ValidationFailed, null, null,
                    [outcome.ErrorMessage ?? "La transición solicitada no es válida."]),
            _ => throw new InvalidOperationException($"Unhandled outcome status '{outcome.Status}'."),
        };
    }

    /// <summary>
    /// Returns a human-readable rejection reason, or null if the authenticated credential is
    /// entitled to register this event as described. ActorType must match exactly (this is the
    /// security-critical check: an AI credential can never claim to be HUMAN). ActorId is only
    /// cross-checked when the credential itself pins one (today: required for HUMAN credentials,
    /// optional for CODER/AI_ANALYST) - see docs/aura/AURA_PROTOCOL_v2.1.md.
    /// </summary>
    private static string? CheckAuthorization(
        RegisterProjectEventRequest request, ProjectActorType actorType, AuthenticatedActor authenticatedActor)
    {
        if (request.TenantId != authenticatedActor.TenantId)
        {
            return "La credencial autenticada no pertenece a este tenantId.";
        }

        if (actorType != authenticatedActor.ActorType)
        {
            return $"La credencial autenticada es {authenticatedActor.ActorType}; no puede registrar un evento como actor.type '{ProjectControlCodes.ToCode(actorType)}'.";
        }

        if (authenticatedActor.LinkedActorId is { } linkedId && request.Actor.Id != linkedId)
        {
            return "actor.id no coincide con la identidad vinculada a la credencial autenticada.";
        }

        return null;
    }

    private static LeaseInfoDto? ToLeaseInfo(LeaseSnapshot? lease) => lease is null
        ? null
        : new LeaseInfoDto(
            lease.LeaseId, lease.Version, lease.ExpiresAtUtc,
            ProjectControlCodes.ToCode(lease.HolderActorType), lease.HolderActorId);

    private static List<string> Validate(
        RegisterProjectEventRequest request,
        out ProjectEventType eventType,
        out ProjectWorkStatus status,
        out ProjectActorType actorType,
        out ProjectActorType? nextControlType)
    {
        var errors = new List<string>();

        if (request.TenantId <= 0)
        {
            errors.Add("tenantId debe ser mayor a cero.");
        }

        if (string.IsNullOrWhiteSpace(request.ProjectCode))
        {
            errors.Add("projectCode es obligatorio.");
        }

        if (request.EventId == Guid.Empty)
        {
            errors.Add("eventId debe ser un GUID válido.");
        }

        if (string.IsNullOrWhiteSpace(request.Summary))
        {
            errors.Add("summary es obligatorio.");
        }
        else if (request.Summary.Length > SummaryMaxLength)
        {
            errors.Add($"summary no puede superar {SummaryMaxLength} caracteres.");
        }

        ValidateOptionalLength(request.NextAction, nameof(request.NextAction), errors);
        ValidateOptionalLength(request.Blocker, nameof(request.Blocker), errors);
        ValidateOptionalLength(request.Reference, nameof(request.Reference), errors);

        if (request.Model is { } model && model.Trim().Length > ModelMaxLength)
        {
            errors.Add($"model no puede superar {ModelMaxLength} caracteres.");
        }

        if (request.Stage is { } stage && stage.Trim().Length > StageMaxLength)
        {
            errors.Add($"stage no puede superar {StageMaxLength} caracteres.");
        }

        if (request.StageCode is { } stageCode && stageCode.Trim().Length > StageCodeMaxLength)
        {
            errors.Add($"stageCode no puede superar {StageCodeMaxLength} caracteres.");
        }

        if (request.StageOutcome is { } stageOutcome
            && stageOutcome.Trim().ToUpperInvariant() is not ("PASS" or "PASS_WITH_WARNINGS"))
        {
            errors.Add("stageOutcome debe ser PASS o PASS_WITH_WARNINGS.");
        }

        var isApprovalEvent = string.Equals(request.EventType?.Trim(), "APPROVAL", StringComparison.OrdinalIgnoreCase);
        if (isApprovalEvent && request.StageCode is not null && request.StageOutcome is null)
        {
            errors.Add("stageOutcome es obligatorio al aprobar una etapa de Roadmap.");
        }

        if (request.StageOutcome is not null && !isApprovalEvent)
        {
            errors.Add("stageOutcome solo se admite en eventos APPROVAL.");
        }

        if (request.Environment is { } environment && environment.Trim().Length > EnvironmentMaxLength)
        {
            errors.Add($"environment no puede superar {EnvironmentMaxLength} caracteres.");
        }

        if (request.TotalStages is { } totalStages && totalStages <= 0)
        {
            errors.Add("totalStages debe ser mayor a cero.");
        }

        ValidatePercentage(request.StageProgress, nameof(request.StageProgress), errors);

        if (!ProjectControlCodes.TryParseEventType(request.EventType, out eventType))
        {
            errors.Add($"eventType '{request.EventType}' no es válido.");
        }

        if (!ProjectControlCodes.TryParseWorkStatus(request.Status, out status))
        {
            errors.Add($"status '{request.Status}' no es válido.");
        }

        actorType = ProjectActorType.Unassigned;
        if (request.Actor is null || !ProjectControlCodes.TryParseActorType(request.Actor.Type, out actorType))
        {
            errors.Add("actor.type no es válido.");
        }
        else if (actorType != ProjectActorType.Unassigned && request.Actor.Id is null)
        {
            errors.Add("actor.id es obligatorio cuando actor.type no es UNASSIGNED.");
        }

        if (eventType == ProjectEventType.Request && actorType != ProjectActorType.Human)
        {
            errors.Add($"eventType '{request.EventType}' solo puede registrarlo un actor HUMAN.");
        }

        if (eventType == ProjectEventType.Approval)
        {
            var requiredActor = request.StageCode is null ? ProjectActorType.Human : ProjectActorType.AiAnalyst;
            if (actorType != requiredActor)
            {
                errors.Add(request.StageCode is null
                    ? "APPROVAL de etapa legacy requiere actor HUMAN."
                    : "APPROVAL de una etapa de Roadmap requiere actor AI_ANALYST designado.");
            }
        }

        if (request.Agent is { } agent && agent.Trim().Length > EnvironmentMaxLength)
        {
            errors.Add($"agent no puede superar {EnvironmentMaxLength} caracteres.");
        }

        // Toda intervencion no humana debe poder identificarse: agente, modelo y entorno
        // no son opcionales para CODER/AI_ANALYST (si son UNASSIGNED, no aplica).
        if (actorType is ProjectActorType.Coder or ProjectActorType.AiAnalyst)
        {
            if (string.IsNullOrWhiteSpace(request.Agent))
            {
                errors.Add("agent es obligatorio para intervenciones no humanas.");
            }
            if (string.IsNullOrWhiteSpace(request.Model))
            {
                errors.Add("model es obligatorio para intervenciones no humanas.");
            }
            if (string.IsNullOrWhiteSpace(request.Environment))
            {
                errors.Add("environment es obligatorio para intervenciones no humanas.");
            }
        }

        if (request.WorkStream is { } workStream && workStream.Trim().Length > StageMaxLength)
        {
            errors.Add($"workStream no puede superar {StageMaxLength} caracteres.");
        }

        // The lease governs exclusive WORK (CLAIM/PROGRESS/HANDOFF/BLOCKER/DEPLOYMENT): every one
        // of those but CLAIM (which acquires it) must present the lease it holds. REQUEST,
        // APPROVAL, REWORK and COMPLETION are HUMAN governance actions layered on top of that
        // lease, not part of it - a human doesn't need to hold the coder's work lease to
        // exercise approval/rework/completion authority. See docs/aura/AURA_PROTOCOL_v2.1.md
        // "Lease y concurrencia optimista".
        if (eventType is ProjectEventType.Progress or ProjectEventType.Handoff
            or ProjectEventType.Blocker or ProjectEventType.Deployment)
        {
            if (request.LeaseId is null || request.ExpectedLeaseVersion is null)
            {
                errors.Add("leaseId y expectedLeaseVersion son obligatorios para este tipo de evento (tomá el control con CLAIM primero).");
            }
        }

        nextControlType = null;
        if (request.NextControl is not null)
        {
            if (!ProjectControlCodes.TryParseActorType(request.NextControl.Type, out var parsedControlType))
            {
                errors.Add($"nextControl.type '{request.NextControl.Type}' no es válido.");
            }
            else
            {
                nextControlType = parsedControlType;
                if (parsedControlType != ProjectActorType.Unassigned && request.NextControl.Id is null)
                {
                    errors.Add("nextControl.id es obligatorio cuando nextControl.type no es UNASSIGNED.");
                }
            }
        }

        return errors;
    }

    private static void ValidateOptionalLength(string? value, string fieldName, List<string> errors)
    {
        if (value is { Length: > LongTextMaxLength })
        {
            errors.Add($"{fieldName} no puede superar {LongTextMaxLength} caracteres.");
        }
    }

    private static void ValidatePercentage(decimal? value, string fieldName, List<string> errors)
    {
        if (value is not { } percentage)
        {
            return;
        }

        if (percentage < 0 || percentage > 100)
        {
            errors.Add($"{fieldName} debe estar entre 0 y 100.");
            return;
        }

        if (decimal.Truncate(percentage * 100m) != percentage * 100m)
        {
            errors.Add($"{fieldName} admite hasta dos decimales.");
        }
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public async Task<CreateProjectResult> CreateProjectAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateCreateProject(request, out var code, out var status);

        if (errors.Count > 0)
        {
            return new CreateProjectResult(CreateProjectResultStatus.ValidationFailed, null, errors);
        }

        var command = new CreateProjectCommand(
            request.TenantId,
            code,
            request.ProjectName.Trim(),
            NullIfWhiteSpace(request.Description),
            NullIfWhiteSpace(request.WorkingPath),
            NullIfWhiteSpace(request.Repository),
            status,
            request.HumanResponsibleId);

        var outcome = await repository.CreateProjectAsync(command, cancellationToken);

        return outcome.Status switch
        {
            CreateProjectOutcomeStatus.Created =>
                new CreateProjectResult(CreateProjectResultStatus.Created, outcome.ProjectId, null),
            CreateProjectOutcomeStatus.AlreadyExists =>
                new CreateProjectResult(CreateProjectResultStatus.AlreadyExists, outcome.ProjectId, null),
            CreateProjectOutcomeStatus.Conflict =>
                new CreateProjectResult(CreateProjectResultStatus.Conflict, outcome.ProjectId, null),
            _ => throw new InvalidOperationException($"Unhandled outcome status '{outcome.Status}'."),
        };
    }

    private static List<string> ValidateCreateProject(
        CreateProjectRequest request,
        out string code,
        out ProjectStatus status)
    {
        var errors = new List<string>();

        if (request.TenantId <= 0)
        {
            errors.Add("tenantId debe ser mayor a cero.");
        }

        code = string.IsNullOrWhiteSpace(request.ProjectCode) ? string.Empty : request.ProjectCode.Trim().ToUpperInvariant();
        if (code.Length == 0)
        {
            errors.Add("projectCode es obligatorio.");
        }
        else if (code.Length > ProjectCodeMaxLength)
        {
            errors.Add($"projectCode no puede superar {ProjectCodeMaxLength} caracteres.");
        }

        if (string.IsNullOrWhiteSpace(request.ProjectName))
        {
            errors.Add("projectName es obligatorio.");
        }
        else if (request.ProjectName.Trim().Length > ProjectNameMaxLength)
        {
            errors.Add($"projectName no puede superar {ProjectNameMaxLength} caracteres.");
        }

        if (request.Description is { } description && description.Length > DescriptionMaxLength)
        {
            errors.Add($"description no puede superar {DescriptionMaxLength} caracteres.");
        }

        ValidateOptionalLength(request.WorkingPath, nameof(request.WorkingPath), errors);

        if (request.Repository is { } repository && repository.Length > RepositoryMaxLength)
        {
            errors.Add($"repository no puede superar {RepositoryMaxLength} caracteres.");
        }

        if (request.HumanResponsibleId is { } humanResponsibleId && humanResponsibleId <= 0)
        {
            errors.Add("humanResponsibleId debe ser mayor a cero.");
        }

        if (string.IsNullOrWhiteSpace(request.Status))
        {
            status = ProjectStatus.Active;
        }
        else if (!ProjectControlCodes.TryParseProjectStatus(request.Status, out status))
        {
            errors.Add($"status '{request.Status}' no es válido.");
        }

        return errors;
    }
}
