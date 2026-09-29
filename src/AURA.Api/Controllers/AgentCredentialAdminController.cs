using AURA.Api.Admin;
using AURA.Api.Contracts;
using AURA.Application.Admin;
using AURA.Application.AgentCredential;
using AURA.Domain.ProjectControl;
using Microsoft.AspNetCore.Mvc;

namespace AURA.Api.Controllers;

/// <summary>
/// Administra credenciales técnicas. El secreto se incluye únicamente en la respuesta de creación o rotación.
/// El portal usa JWT humano con autorización tenant-admin; X-Admin-Api-Key se conserva solo para compatibilidad backend.
/// </summary>
[ApiController]
[Route("api/admin/agent-credentials")]
[ServiceFilter(typeof(AdminApiKeyFilter))]
public sealed class AgentCredentialAdminController(
    AgentCredentialService agentCredentialService,
    AdminManagementService adminManagementService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateAgentCredentialRequest request, CancellationToken cancellationToken)
    {
        if (!TenantMatches(request.TenantId)) return Forbid();
        if (string.Equals(request.ActorType?.Trim(), "INGESTION", StringComparison.OrdinalIgnoreCase))
        {
            var result = await adminManagementService.CreateCredentialAsync(
                new AdminCredentialCreateRequest(request.TenantId, request.AgentName, "INGESTION", request.ProjectIds ?? [], null),
                GetHumanId(), cancellationToken);
            return CredentialProvisionResult(result);
        }

        var legacyResult = await agentCredentialService.CreateAsync(request, cancellationToken, GetHumanId());
        return legacyResult.Status switch
        {
            CreateAgentCredentialStatus.Created => StatusCode(StatusCodes.Status201Created,
                new CreateAgentCredentialResponse(legacyResult.CredentialId, legacyResult.PlainSecret, null)),
            CreateAgentCredentialStatus.ValidationFailed => BadRequest(
                new CreateAgentCredentialResponse(null, null, legacyResult.Errors)),
            _ => throw new InvalidOperationException($"Unhandled create status '{legacyResult.Status}'."),
        };
    }

    [HttpPost("{credentialId:int}/rotate")]
    public async Task<IActionResult> Rotate(int credentialId, [FromQuery] int tenantId, CancellationToken cancellationToken)
    {
        if (!TenantMatches(tenantId)) return Forbid();
        return CredentialProvisionResult(await adminManagementService.RotateCredentialAsync(tenantId, credentialId, GetHumanId(), cancellationToken));
    }

    [HttpPost("{credentialId:int}/revoke")]
    public async Task<IActionResult> Revoke(int credentialId, [FromQuery] int tenantId, CancellationToken cancellationToken)
    {
        if (!TenantMatches(tenantId)) return Forbid();
        var result = await adminManagementService.RevokeCredentialAsync(tenantId, credentialId, GetHumanId(), cancellationToken);
        return result.Status switch
        {
            AdminWriteStatus.Succeeded => Ok(new { status = "REVOKED" }),
            AdminWriteStatus.NotFound => NotFound(),
            _ => BadRequest(new { status = "VALIDATION_FAILED", errors = result.Errors }),
        };
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminAgentCredentialDto>>> List([FromQuery] int tenantId, CancellationToken cancellationToken)
    {
        if (!TenantMatches(tenantId)) return Forbid();
        return Ok(await adminManagementService.ListCredentialsAsync(tenantId, cancellationToken));
    }

    [HttpGet("{credentialId:int}/projects")]
    public async Task<ActionResult<IReadOnlyList<AdminCredentialProjectDto>>> ListProjects(int credentialId, [FromQuery] int tenantId, CancellationToken cancellationToken)
    {
        if (!TenantMatches(tenantId)) return Forbid();
        return Ok(await adminManagementService.ListCredentialProjectsAsync(tenantId, credentialId, cancellationToken));
    }

    [HttpPost("{credentialId:int}/projects")]
    public async Task<IActionResult> AssignProject(int credentialId, [FromBody] AssignCredentialProjectRequest request, CancellationToken cancellationToken)
    {
        if (!TenantMatches(request.TenantId)) return Forbid();
        var result = await adminManagementService.AssignProjectAsync(request.TenantId, credentialId, request.ProjectId, GetHumanId(), cancellationToken);
        return result.Status switch
        {
            AdminWriteStatus.Succeeded => StatusCode(StatusCodes.Status201Created, new { projectId = result.Id }),
            AdminWriteStatus.Conflict => Conflict(new { status = "ALREADY_ASSIGNED", errors = result.Errors }),
            AdminWriteStatus.NotFound => NotFound(),
            AdminWriteStatus.ValidationFailed => BadRequest(new { status = "VALIDATION_FAILED", errors = result.Errors }),
            _ => Conflict(),
        };
    }

    [HttpDelete("{credentialId:int}/projects/{projectId:int}")]
    public async Task<IActionResult> RevokeProject(int credentialId, int projectId, [FromQuery] int tenantId, CancellationToken cancellationToken)
    {
        if (!TenantMatches(tenantId)) return Forbid();
        var result = await adminManagementService.RevokeProjectAssignmentAsync(tenantId, credentialId, projectId, GetHumanId(), cancellationToken);
        return result.Status switch
        {
            AdminWriteStatus.Succeeded => NoContent(),
            AdminWriteStatus.NotFound => NotFound(),
            _ => BadRequest(new { status = "VALIDATION_FAILED", errors = result.Errors }),
        };
    }

    private IActionResult CredentialProvisionResult(AdminCredentialCreateResult result) => result.Status switch
    {
        AdminWriteStatus.Succeeded => StatusCode(StatusCodes.Status201Created, result.Credential),
        AdminWriteStatus.NotFound => NotFound(),
        AdminWriteStatus.ProjectScopeRequired => Conflict(new { status = "PROJECT_SCOPE_REQUIRED", errors = result.Errors }),
        AdminWriteStatus.Conflict => Conflict(new { status = "CONFLICT", errors = result.Errors }),
        AdminWriteStatus.ValidationFailed => BadRequest(new { status = "VALIDATION_FAILED", errors = result.Errors }),
        _ => throw new InvalidOperationException($"Unhandled credential operation status '{result.Status}'."),
    };

    private bool TenantMatches(int tenantId) => tenantId > 0 &&
        (User.Identity?.IsAuthenticated != true || GetClaimInt("aura:tenantId") == tenantId);

    private int? GetHumanId() =>
        string.Equals(User.FindFirst("aura:actorType")?.Value, "HUMAN", StringComparison.OrdinalIgnoreCase)
            ? GetClaimInt("aura:linkedActorId")
            : null;

    private int? GetClaimInt(string claimType) =>
        int.TryParse(User.FindFirst(claimType)?.Value, out var value) ? value : null;
}

public sealed record AssignCredentialProjectRequest(int TenantId, int ProjectId);
