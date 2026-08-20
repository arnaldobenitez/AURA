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
