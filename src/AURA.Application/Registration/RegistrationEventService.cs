using AURA.Application.AgentCredential;
using AURA.Domain.ProjectControl;
using AURA.Domain.Registration;

namespace AURA.Application.Registration;

public sealed class RegistrationEventService(IRegistrationEventRepository repository)
{
    private const int ProjectCodeMaxLength = 50;
    private const int SummaryMaxLength = 300;
    private const int DetailsMaxLength = 4000;
    private const int ShortTextMaxLength = 100;
    private const int ClaimedActorTypeMaxLength = 30;
    private const int WorkspaceMaxLength = 300;
    private const int ExternalProjectKeyMaxLength = 200;
    private const int ProjectNameMaxLength = 200;

    public async Task<RegistrationEventResult> RegisterAsync(
        RegisterRegistrationEventRequest request,
        AuthenticatedActor authenticatedActor,
        CancellationToken cancellationToken)
    {
        if (authenticatedActor.ActorType != ProjectActorType.Ingestion)
        {
            return new RegistrationEventResult(
                RegistrationEventStatus.Forbidden,
                request.EventId,
                null,
                null,
                ["El canal REGISTRATION_ONLY requiere una credencial técnica INGESTION."]);
        }

        if (authenticatedActor.TenantId != request.TenantId)
        {
            return new RegistrationEventResult(
                RegistrationEventStatus.Forbidden,
                request.EventId,
                null,
                null,
                ["La credencial autenticada no pertenece a este tenantId."]);
        }

        var errors = Validate(request, out var eventType, out var projectCode);
        if (errors.Count > 0)
        {
            return new RegistrationEventResult(
                RegistrationEventStatus.ValidationFailed,
                request.EventId,
                null,
                null,
                errors);
        }

        if (!await repository.CanCredentialRegisterForProjectAsync(
                authenticatedActor.TenantId, authenticatedActor.CredentialId, projectCode, cancellationToken))
        {
            return new RegistrationEventResult(
                RegistrationEventStatus.Forbidden,
                request.EventId,
                null,
                null,
                ["La credencial está revocada, vencida o no tiene asignado este proyecto."]);
        }

        var command = new RegistrationEventCommand(
            request.EventId,
            request.TenantId,
            projectCode,
            "REGISTRATION_ONLY",
            eventType,
            request.Summary.Trim(),
            TrimToNull(request.Details),
            TrimToNull(request.ClaimedActorType)?.ToUpperInvariant(),
            request.ClaimedActorId,
            TrimToNull(request.AgentName),
            TrimToNull(request.Host),
            TrimToNull(request.Workspace),
            request.SessionId,
            TrimToNull(request.Model),
            TrimToNull(request.Environment),
            TrimToNull(request.Source),
            request.OccurredAtUtc!.Value.UtcDateTime,
            TrimToNull(request.ExternalProjectKey),
            TrimToNull(request.ProjectName),
            authenticatedActor.CredentialId);

        var outcome = await repository.RegisterAsync(command, cancellationToken);
        return new RegistrationEventResult(
            outcome.Status,
            request.EventId,
            outcome.ItemId,
            outcome.ProjectResolution?.ToString().ToUpperInvariant());
    }

    public Task<RegistrationEventDto?> GetAsync(
        int tenantId,
        Guid eventId,
        CancellationToken cancellationToken) =>
        repository.GetAsync(tenantId, eventId, cancellationToken);

    public Task<bool> CanCredentialRegisterForProjectAsync(
        int tenantId,
        int credentialId,
        string projectCode,
        CancellationToken cancellationToken) =>
        repository.CanCredentialRegisterForProjectAsync(tenantId, credentialId, projectCode, cancellationToken);

    private static List<string> Validate(
        RegisterRegistrationEventRequest request,
        out RegistrationEventType eventType,
        out string projectCode)
    {
        var errors = new List<string>();
        eventType = default;
        projectCode = string.Empty;

        if (request.EventId == Guid.Empty)
        {
            errors.Add("eventId debe ser un GUID válido.");
        }

        if (request.TenantId <= 0)
        {
            errors.Add("tenantId debe ser mayor a cero.");
        }

        if (string.IsNullOrWhiteSpace(request.ProjectCode))
        {
            errors.Add("projectCode es obligatorio.");
        }
        else if (request.ProjectCode.Trim().Length > ProjectCodeMaxLength)
        {
            errors.Add($"projectCode no puede superar {ProjectCodeMaxLength} caracteres.");
        }
        else
        {
            projectCode = request.ProjectCode.Trim().ToUpperInvariant();
        }

        if (!string.Equals(request.Mode?.Trim(), "REGISTRATION_ONLY", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("mode debe ser REGISTRATION_ONLY; GOVERNED_EXECUTION usa POST /api/project-control/events.");
        }

        if (!TryParseEventType(request.EventType, out eventType))
        {
            errors.Add("eventType debe ser INFO, PROGRESS, DECISION, BLOCKER, HANDOFF, REQUEST, NOTE o ERROR.");
        }

        if (string.IsNullOrWhiteSpace(request.Summary))
        {
            errors.Add("summary es obligatorio.");
        }
        else if (request.Summary.Trim().Length > SummaryMaxLength)
        {
            errors.Add($"summary no puede superar {SummaryMaxLength} caracteres.");
        }

        ValidateLength(request.Details, nameof(request.Details), DetailsMaxLength, errors);
        ValidateLength(request.ClaimedActorType, nameof(request.ClaimedActorType), ClaimedActorTypeMaxLength, errors);
        ValidateLength(request.AgentName, nameof(request.AgentName), ShortTextMaxLength, errors);
        ValidateLength(request.Host, nameof(request.Host), ShortTextMaxLength, errors);
        ValidateLength(request.Workspace, nameof(request.Workspace), WorkspaceMaxLength, errors);
        ValidateLength(request.Model, nameof(request.Model), ShortTextMaxLength, errors);
        ValidateLength(request.Environment, nameof(request.Environment), ShortTextMaxLength, errors);
        ValidateLength(request.Source, nameof(request.Source), ShortTextMaxLength, errors);
        ValidateLength(request.ExternalProjectKey, nameof(request.ExternalProjectKey), ExternalProjectKeyMaxLength, errors);
        ValidateLength(request.ProjectName, nameof(request.ProjectName), ProjectNameMaxLength, errors);

        if (request.ClaimedActorId is { } claimedActorId && claimedActorId <= 0)
        {
            errors.Add("claimedActorId debe ser mayor a cero.");
        }

        if (request.SessionId == Guid.Empty)
        {
            errors.Add("sessionId debe ser un GUID válido cuando se informa.");
        }

        if (request.OccurredAtUtc is null)
        {
            errors.Add("occurredAtUtc es obligatorio.");
        }

        if (!string.Equals(request.IdentityStatus?.Trim(), "UNVERIFIED", StringComparison.OrdinalIgnoreCase)
            && request.IdentityStatus is not null)
        {
            errors.Add("identityStatus, si se informa, debe ser UNVERIFIED.");
        }

        ValidateSecrets(request, errors);
        return errors;
    }

    private static void ValidateLength(string? value, string fieldName, int maxLength, List<string> errors)
    {
        if (value is { } text && text.Trim().Length > maxLength)
        {
            errors.Add($"{fieldName} no puede superar {maxLength} caracteres.");
        }
    }

    private static void ValidateSecrets(RegisterRegistrationEventRequest request, List<string> errors)
    {
        foreach (var (value, fieldName) in new (string? Value, string FieldName)[]
        {
            (request.ProjectCode, nameof(request.ProjectCode)),
            (request.Mode, nameof(request.Mode)),
            (request.EventType, nameof(request.EventType)),
            (request.Summary, nameof(request.Summary)),
            (request.Details, nameof(request.Details)),
            (request.ClaimedActorType, nameof(request.ClaimedActorType)),
            (request.AgentName, nameof(request.AgentName)),
            (request.Host, nameof(request.Host)),
            (request.Workspace, nameof(request.Workspace)),
            (request.Model, nameof(request.Model)),
            (request.Environment, nameof(request.Environment)),
            (request.Source, nameof(request.Source)),
            (request.ExternalProjectKey, nameof(request.ExternalProjectKey)),
            (request.ProjectName, nameof(request.ProjectName)),
            (request.IdentityStatus, nameof(request.IdentityStatus)),
        })
        {
            if (SensitiveContentValidator.ContainsSecret(value))
            {
                errors.Add($"{fieldName} contiene material sensible y no puede registrarse.");
            }
        }
    }

    private static bool TryParseEventType(string? value, out RegistrationEventType eventType)
    {
        switch (value?.Trim().ToUpperInvariant())
        {
            case "INFO": eventType = RegistrationEventType.Info; return true;
            case "PROGRESS": eventType = RegistrationEventType.Progress; return true;
            case "DECISION": eventType = RegistrationEventType.Decision; return true;
            case "BLOCKER": eventType = RegistrationEventType.Blocker; return true;
            case "HANDOFF": eventType = RegistrationEventType.Handoff; return true;
            case "REQUEST": eventType = RegistrationEventType.Request; return true;
            case "NOTE": eventType = RegistrationEventType.Note; return true;
            case "ERROR": eventType = RegistrationEventType.Error; return true;
            default: eventType = default; return false;
        }
    }

    private static string? TrimToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
