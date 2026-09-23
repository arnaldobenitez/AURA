using AURA.Api.Contracts;
using AURA.Application.AgentCredential;
using AURA.Application.ProjectControl;
using AURA.Domain.ProjectControl;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AURA.Api.Controllers;

// Login básico (2026-08-28): tanto lectura como escritura exigen ahora un JWT válido - un
// humano debe autenticarse (POST /api/auth/login) para poder ver el tablero, no solo para
// escribir. Ver docs/aura/AURA_PROTOCOL_v2.2.md.
[ApiController]
[Route("api/project-control")]
[Authorize]
public sealed class ProjectControlController(ProjectControlService projectControlService) : ControllerBase
{
    /// <summary>
    /// Deriva la identidad autenticada a partir de los claims del JWT ya validado por el
    /// middleware - nunca del cuerpo del request. Ver AuraJwtClaimTypes/JwtAccessTokenIssuer.
    /// </summary>
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

    [HttpGet]
    public async Task<ActionResult<ProjectControlBoardDto>> GetBoard(
        [FromQuery] int tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId <= 0)
        {
            return Problem(
                title: "tenantId inválido.",
                detail: "tenantId debe ser un entero mayor a cero.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var projects = await projectControlService.GetBoardAsync(tenantId, cancellationToken);

        return Ok(new ProjectControlBoardDto(tenantId, DateTime.UtcNow, projects));
    }

    [HttpGet("projects/{projectCode}/events")]
    public async Task<ActionResult<ProjectHistoryDto>> GetHistory(
        string projectCode,
        [FromQuery] int tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId <= 0)
        {
            return Problem(
                title: "tenantId inválido.",
                detail: "tenantId debe ser un entero mayor a cero.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var history = await projectControlService.GetHistoryAsync(tenantId, projectCode, cancellationToken);
        if (history is null)
        {
            return Problem(
                title: "Proyecto no encontrado.",
                detail: "No existe un proyecto con ese tenantId y projectCode.",
                statusCode: StatusCodes.Status404NotFound);
        }

        return Ok(history);
    }

    [HttpGet("projects/{projectCode}/roadmap")]
    public async Task<ActionResult<ProjectRoadmapDto>> GetRoadmap(
        string projectCode,
        [FromQuery] int tenantId,
        [FromQuery] string? workStream,
        CancellationToken cancellationToken)
    {
        if (tenantId <= 0)
        {
            return Problem(
                title: "tenantId inválido.",
                detail: "tenantId debe ser un entero mayor a cero.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var roadmap = await projectControlService.GetRoadmapAsync(tenantId, projectCode, workStream, cancellationToken);
        return roadmap is null
            ? Problem(title: "Proyecto no encontrado.", detail: "No existe un proyecto con ese tenantId y projectCode.", statusCode: StatusCodes.Status404NotFound)
            : Ok(roadmap);
    }

    [HttpPut("projects/{projectCode}/roadmap")]
    public async Task<IActionResult> UpdateRoadmap(
        string projectCode,
        [FromBody] UpdateProjectRoadmapRequest request,
        CancellationToken cancellationToken)
    {
        var result = await projectControlService.UpdateRoadmapAsync(projectCode, request, GetAuthenticatedActor(), cancellationToken);
        return result.Status switch
        {
            UpdateProjectRoadmapStatus.Updated => Ok(new { status = "UPDATED" }),
            UpdateProjectRoadmapStatus.ProjectNotFound => Problem(title: "Proyecto no encontrado.", statusCode: StatusCodes.Status404NotFound),
            UpdateProjectRoadmapStatus.ExecNotFound => Problem(title: "No hay un EXEC activo para este Roadmap.", statusCode: StatusCodes.Status409Conflict),
            UpdateProjectRoadmapStatus.Forbidden => Problem(title: "Solo una credencial HUMAN del tenant puede administrar el Roadmap.", statusCode: StatusCodes.Status403Forbidden),
            UpdateProjectRoadmapStatus.PlanLocked => Conflict(new { status = "PLAN_LOCKED", errors = result.Errors }),
            UpdateProjectRoadmapStatus.InvalidPlan => BadRequest(new { status = "VALIDATION_FAILED", errors = result.Errors }),
            _ => throw new InvalidOperationException($"Unhandled roadmap update status '{result.Status}'."),
        };
    }

    [HttpPost("projects")]
    public async Task<IActionResult> CreateProject(
        [FromBody] CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var result = await projectControlService.CreateProjectAsync(request, cancellationToken);

        switch (result.Status)
        {
            case CreateProjectResultStatus.Created:
                return StatusCode(
                    StatusCodes.Status201Created,
                    new CreateProjectResponse("CREATED", result.ProjectId));

            case CreateProjectResultStatus.AlreadyExists:
                return Ok(new CreateProjectResponse("ALREADY_EXISTS", result.ProjectId));

            case CreateProjectResultStatus.Conflict:
                return Conflict(new CreateProjectResponse("CONFLICT", result.ProjectId));

            case CreateProjectResultStatus.ValidationFailed:
                return BadRequest(new ProjectControlValidationProblemResponse(
                    "VALIDATION_FAILED", result.ValidationErrors ?? []));

            default:
                throw new InvalidOperationException($"Unhandled result status '{result.Status}'.");
        }
    }

    [HttpPost("events")]
    public async Task<IActionResult> RegisterEvent(
        [FromBody] RegisterProjectEventRequest request,
        CancellationToken cancellationToken)
    {
        var result = await projectControlService.RegisterEventAsync(request, GetAuthenticatedActor(), cancellationToken);

        switch (result.Status)
        {
            case RegisterProjectEventStatus.Created:
                return StatusCode(
                    StatusCodes.Status201Created,
                    new ProjectControlEventResponse("CREATED", result.ItemId, null, result.Lease));

            case RegisterProjectEventStatus.AlreadyExists:
                return Ok(new ProjectControlEventResponse("ALREADY_EXISTS", result.ItemId, null, result.Lease));

            case RegisterProjectEventStatus.ProjectNotFound:
                return Problem(
                    title: "Proyecto no encontrado.",
                    detail: "No existe un proyecto con ese tenantId y projectCode.",
                    statusCode: StatusCodes.Status404NotFound);

            case RegisterProjectEventStatus.Conflict:
                return Conflict(new ProjectControlEventResponse("CONFLICT", null, result.CurrentLastItemId));

            case RegisterProjectEventStatus.LeaseConflict:
                return Conflict(new ProjectControlEventResponse("LEASE_CONFLICT", null, null, result.Lease));

            case RegisterProjectEventStatus.Forbidden:
                return Problem(
                    title: "No autorizado.",
                    detail: result.ValidationErrors is { Count: > 0 } ? result.ValidationErrors[0] : "La credencial autenticada no puede registrar este evento.",
                    statusCode: StatusCodes.Status403Forbidden);

            case RegisterProjectEventStatus.ValidationFailed:
                return BadRequest(new ProjectControlValidationProblemResponse(
                    "VALIDATION_FAILED", result.ValidationErrors ?? []));

            default:
                throw new InvalidOperationException($"Unhandled result status '{result.Status}'.");
        }
    }
}
