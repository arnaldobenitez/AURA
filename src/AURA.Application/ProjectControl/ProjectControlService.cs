using AURA.Domain.ProjectControl;

namespace AURA.Application.ProjectControl;

public sealed class ProjectControlService(IProjectControlRepository repository)
{
    private const int SummaryMaxLength = 300;
    private const int LongTextMaxLength = 1000;
    private const int ModelMaxLength = 100;

    public Task<IReadOnlyList<ProjectControlProjectDto>> GetBoardAsync(int tenantId, CancellationToken cancellationToken)
    {
        return repository.GetBoardAsync(tenantId, cancellationToken);
    }

    public async Task<RegisterProjectEventResult> RegisterEventAsync(
        RegisterProjectEventRequest request,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request, out var eventType, out var status, out var actorType, out var nextControlType);

        if (errors.Count > 0)
        {
            return new RegisterProjectEventResult(RegisterProjectEventStatus.ValidationFailed, null, null, errors);
        }

        var command = new ProjectEventCommand(
            request.TenantId,
            request.ProjectCode.Trim(),
            request.EventId,
            request.ExpectedPreviousItemId,
            eventType,
            actorType,
            request.Actor.Id,
            status,
            request.Summary.Trim(),
            NullIfWhiteSpace(request.Details),
            nextControlType,
            request.NextControl?.Id,
            NullIfWhiteSpace(request.NextAction),
            NullIfWhiteSpace(request.Blocker),
            NullIfWhiteSpace(request.Reference),
            NullIfWhiteSpace(request.Model));

        var outcome = await repository.RegisterEventAsync(command, cancellationToken);

        return outcome.Status switch
        {
            ProjectEventOutcomeStatus.Created =>
                new RegisterProjectEventResult(RegisterProjectEventStatus.Created, outcome.ItemId, null, null),
            ProjectEventOutcomeStatus.AlreadyExists =>
                new RegisterProjectEventResult(RegisterProjectEventStatus.AlreadyExists, outcome.ItemId, null, null),
            ProjectEventOutcomeStatus.ProjectNotFound =>
                new RegisterProjectEventResult(RegisterProjectEventStatus.ProjectNotFound, null, null, null),
            ProjectEventOutcomeStatus.Conflict =>
                new RegisterProjectEventResult(RegisterProjectEventStatus.Conflict, null, outcome.CurrentLastItemId, null),
            _ => throw new InvalidOperationException($"Unhandled outcome status '{outcome.Status}'."),
        };
    }

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

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
