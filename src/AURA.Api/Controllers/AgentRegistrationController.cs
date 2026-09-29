using AURA.Api.Contracts;
using AURA.Application.AgentCredential;
using AURA.Application.ProjectControl;
using AURA.Application.Registration;
using AURA.Domain.ProjectControl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AURA.Api.Controllers;

/// <summary>
/// AURA Agent Registration Protocol v2.3. This channel is append-only and accepts only
/// technical INGESTION credentials. It intentionally has no access to governed execution
/// state changes; GOVERNED_EXECUTION remains on POST /api/project-control/events.
/// </summary>
[ApiController]
[Route("api/agent-registration/events")]
[Authorize]
public sealed class AgentRegistrationController(RegistrationEventService registrationEventService) : ControllerBase
{
    private AuthenticatedActor GetAuthenticatedActor()
    {
        var user = HttpContext.User;
        var tenantId = int.Parse(user.FindFirst(AuraJwtClaimTypes.TenantId)!.Value);
        var credentialId = int.Parse(user.FindFirst(AuraJwtClaimTypes.CredentialId)!.Value);
        var actorTypeCode = user.FindFirst(AuraJwtClaimTypes.ActorType)!.Value;
        ProjectControlCodes.TryParseActorType(actorTypeCode, out var actorType);
        var linkedActorIdClaim = user.FindFirst(AuraJwtClaimTypes.LinkedActorId)?.Value;
        var agentName = user.FindFirst(AuraJwtClaimTypes.AgentName)?.Value ?? string.Empty;

        return new AuthenticatedActor(
            tenantId, credentialId, actorType,
            linkedActorIdClaim is null ? null : int.Parse(linkedActorIdClaim),
            agentName);
    }

    [HttpPost]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRegistrationEventRequest request,
        CancellationToken cancellationToken)
    {
        var result = await registrationEventService.RegisterAsync(
            request,
            GetAuthenticatedActor(),
            cancellationToken);

        return result.Status switch
        {
            RegistrationEventStatus.Created => StatusCode(
                StatusCodes.Status201Created,
                new RegistrationEventResponse("CREATED", result.EventId, result.ItemId, result.ProjectResolution)),
            RegistrationEventStatus.AlreadyExists => Ok(
                new RegistrationEventResponse("ALREADY_EXISTS", result.EventId, result.ItemId, result.ProjectResolution)),
            RegistrationEventStatus.Forbidden => Problem(
                title: "No autorizado.",
                detail: result.ValidationErrors is { Count: > 0 }
                    ? result.ValidationErrors[0]
                    : "La credencial autenticada no puede registrar eventos de ingestión.",
                statusCode: StatusCodes.Status403Forbidden),
            RegistrationEventStatus.ValidationFailed => BadRequest(
                new ProjectControlValidationProblemResponse("VALIDATION_FAILED", result.ValidationErrors ?? [])),
            _ => throw new InvalidOperationException($"Unhandled result status '{result.Status}'."),
        };
    }

    [HttpGet("{eventId:guid}")]
    public async Task<ActionResult<RegistrationEventDto>> Get(
        Guid eventId,
        [FromQuery] int tenantId,
        CancellationToken cancellationToken)
    {
        var actor = GetAuthenticatedActor();
        if (actor.ActorType != ProjectActorType.Ingestion || actor.TenantId != tenantId)
        {
            return Problem(
                title: "No autorizado.",
                detail: "El read-back de REGISTRATION_ONLY requiere una credencial INGESTION del mismo tenant.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var registrationEvent = await registrationEventService.GetAsync(tenantId, eventId, cancellationToken);
        if (registrationEvent is null)
        {
            return NotFound();
        }

        if (!await registrationEventService.CanCredentialRegisterForProjectAsync(tenantId, actor.CredentialId, registrationEvent.ProjectCode, cancellationToken))
        {
            return Problem(
                title: "No autorizado.",
                detail: "La credencial está revocada, vencida o no tiene asignado este proyecto.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        return Ok(registrationEvent);
    }
}
