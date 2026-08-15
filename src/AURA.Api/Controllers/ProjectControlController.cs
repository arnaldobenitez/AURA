using AURA.Api.Contracts;
using AURA.Application.ProjectControl;
using Microsoft.AspNetCore.Mvc;

namespace AURA.Api.Controllers;

// NOTE: this endpoint has no authentication yet. It must not be exposed outside a
// controlled/trusted environment until real auth is added (see README).
[ApiController]
[Route("api/project-control")]
public sealed class ProjectControlController(ProjectControlService projectControlService) : ControllerBase
{
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

    [HttpPost("events")]
    public async Task<IActionResult> RegisterEvent(
        [FromBody] RegisterProjectEventRequest request,
        CancellationToken cancellationToken)
    {
        var result = await projectControlService.RegisterEventAsync(request, cancellationToken);

        switch (result.Status)
        {
            case RegisterProjectEventStatus.Created:
                return StatusCode(
                    StatusCodes.Status201Created,
                    new ProjectControlEventResponse("CREATED", result.ItemId, null));

            case RegisterProjectEventStatus.AlreadyExists:
                return Ok(new ProjectControlEventResponse("ALREADY_EXISTS", result.ItemId, null));

            case RegisterProjectEventStatus.ProjectNotFound:
                return Problem(
                    title: "Proyecto no encontrado.",
                    detail: "No existe un proyecto con ese tenantId y projectCode.",
                    statusCode: StatusCodes.Status404NotFound);

            case RegisterProjectEventStatus.Conflict:
                return Conflict(new ProjectControlEventResponse("CONFLICT", null, result.CurrentLastItemId));

            case RegisterProjectEventStatus.ValidationFailed:
                return BadRequest(new ProjectControlValidationProblemResponse(
                    "VALIDATION_FAILED", result.ValidationErrors ?? []));

            default:
                throw new InvalidOperationException($"Unhandled result status '{result.Status}'.");
        }
    }
}
