using System.Security.Claims;
using AURA.Api.Admin;
using AURA.Application.Admin;
using Microsoft.AspNetCore.Mvc;

namespace AURA.Api.Controllers;

[ApiController]
[Route("api/admin")]
[ServiceFilter(typeof(AdminApiKeyFilter))]
public sealed class AdminProjectsController(AdminManagementService adminManagementService) : ControllerBase
{
    [HttpGet("authorization")]
    public IActionResult GetAuthorization() => Ok(new
    {
        tenantId = GetClaimInt("aura:tenantId"),
        userId = GetClaimInt("aura:linkedActorId"),
        isAdmin = true,
    });

    [HttpGet("projects")]
    public async Task<ActionResult<IReadOnlyList<AdminProjectDto>>> ListProjects([FromQuery] int tenantId, CancellationToken cancellationToken)
    {
        if (!TenantMatches(tenantId)) return Forbid();
        return Ok(await adminManagementService.ListProjectsAsync(tenantId, cancellationToken));
    }

    [HttpPost("projects")]
    public async Task<IActionResult> CreateProject([FromBody] AdminProjectCreateRequest request, CancellationToken cancellationToken)
    {
        if (!TenantMatches(request.TenantId)) return Forbid();
        var result = await adminManagementService.CreateProjectAsync(request, GetClaimInt("aura:linkedActorId"), cancellationToken);
        return result.Status switch
        {
            AdminWriteStatus.Succeeded => StatusCode(StatusCodes.Status201Created, await adminManagementService.GetProjectAsync(request.TenantId, result.Id!.Value, cancellationToken)),
            AdminWriteStatus.Conflict => Conflict(new { status = "CONFLICT", errors = result.Errors }),
            AdminWriteStatus.ValidationFailed => BadRequest(new { status = "VALIDATION_FAILED", errors = result.Errors }),
            _ => throw new InvalidOperationException($"Unhandled project create status '{result.Status}'."),
        };
    }

    [HttpPut("projects/{projectId:int}")]
    public async Task<IActionResult> UpdateProject(int projectId, [FromBody] AdminProjectUpdateRequest request, CancellationToken cancellationToken)
    {
        if (!TenantMatches(request.TenantId)) return Forbid();
        var result = await adminManagementService.UpdateProjectAsync(request.TenantId, projectId, request, GetClaimInt("aura:linkedActorId"), cancellationToken);
        return result.Status switch
        {
            AdminWriteStatus.Succeeded => Ok(await adminManagementService.GetProjectAsync(request.TenantId, projectId, cancellationToken)),
            AdminWriteStatus.NotFound => NotFound(),
            AdminWriteStatus.ValidationFailed => BadRequest(new { status = "VALIDATION_FAILED", errors = result.Errors }),
            _ => Conflict(new { status = "CONFLICT", errors = result.Errors }),
        };
    }

    [HttpPost("projects/{projectId:int}/activate")]
    public Task<IActionResult> ActivateProject(int projectId, [FromQuery] int tenantId, CancellationToken cancellationToken) =>
        SetStatusAsync(tenantId, projectId, "ACTIVE", cancellationToken);

    [HttpPost("projects/{projectId:int}/pause")]
    public Task<IActionResult> PauseProject(int projectId, [FromQuery] int tenantId, CancellationToken cancellationToken) =>
        SetStatusAsync(tenantId, projectId, "PAUSED", cancellationToken);

    [HttpPost("projects/{projectId:int}/deactivate")]
    public Task<IActionResult> DeactivateProject(int projectId, [FromQuery] int tenantId, CancellationToken cancellationToken) =>
        SetStatusAsync(tenantId, projectId, "ARCHIVED", cancellationToken);

    private async Task<IActionResult> SetStatusAsync(int tenantId, int projectId, string status, CancellationToken cancellationToken)
    {
        if (!TenantMatches(tenantId)) return Forbid();
        var result = await adminManagementService.SetProjectStatusAsync(tenantId, projectId, status, GetClaimInt("aura:linkedActorId"), cancellationToken);
        return result.Status switch
        {
            AdminWriteStatus.Succeeded => Ok(await adminManagementService.GetProjectAsync(tenantId, projectId, cancellationToken)),
            AdminWriteStatus.NotFound => NotFound(),
            _ => BadRequest(new { status = "VALIDATION_FAILED", errors = result.Errors }),
        };
    }

    private bool TenantMatches(int tenantId) => tenantId > 0 &&
        (User.Identity?.IsAuthenticated != true || GetClaimInt("aura:tenantId") == tenantId);

    private int? GetClaimInt(string claimType) =>
        int.TryParse(User.FindFirstValue(claimType), out var value) ? value : null;
}
