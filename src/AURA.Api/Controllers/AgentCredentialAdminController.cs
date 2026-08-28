using AURA.Api.Admin;
using AURA.Api.Contracts;
using AURA.Application.AgentCredential;
using Microsoft.AspNetCore.Mvc;

namespace AURA.Api.Controllers;

/// <summary>
/// Administrative operations on AgentCredential: create, revoke, rotate, list metadata. Never
/// returns a secret except once, at creation/rotation time. Protected by a static admin API key
/// (see AdminApiKeyFilter), not by AgentCredential/JWT itself - see
/// docs/aura/AURA_PROTOCOL_v2.1.md "Administración de credenciales".
/// </summary>
[ApiController]
[Route("api/admin/agent-credentials")]
[ServiceFilter(typeof(AdminApiKeyFilter))]
public sealed class AgentCredentialAdminController(AgentCredentialService agentCredentialService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CreateAgentCredentialResponse>> Create(
        [FromBody] CreateAgentCredentialRequest request, CancellationToken cancellationToken)
    {
        var result = await agentCredentialService.CreateAsync(request, cancellationToken);

        return result.Status switch
        {
            CreateAgentCredentialStatus.Created => StatusCode(
                StatusCodes.Status201Created,
                new CreateAgentCredentialResponse(result.CredentialId, result.PlainSecret, null)),
            CreateAgentCredentialStatus.ValidationFailed => BadRequest(
                new CreateAgentCredentialResponse(null, null, result.Errors)),
            _ => throw new InvalidOperationException($"Unhandled status '{result.Status}'."),
        };
    }

    [HttpPost("{credentialId:int}/rotate")]
    public async Task<ActionResult<CreateAgentCredentialResponse>> Rotate(
        int credentialId, [FromQuery] int tenantId, CancellationToken cancellationToken)
    {
        var result = await agentCredentialService.RotateAsync(tenantId, credentialId, cancellationToken);

        return result.Status switch
        {
            CreateAgentCredentialStatus.Created => Ok(
                new CreateAgentCredentialResponse(result.CredentialId, result.PlainSecret, null)),
            CreateAgentCredentialStatus.ValidationFailed => NotFound(
                new CreateAgentCredentialResponse(null, null, result.Errors)),
            _ => throw new InvalidOperationException($"Unhandled status '{result.Status}'."),
        };
    }

    [HttpPost("{credentialId:int}/revoke")]
    public async Task<IActionResult> Revoke(
        int credentialId, [FromQuery] int tenantId, CancellationToken cancellationToken)
    {
        var result = await agentCredentialService.RevokeAsync(new RevokeAgentCredentialRequest(tenantId, credentialId), cancellationToken);

        return result.Status switch
        {
            RevokeAgentCredentialStatus.Revoked => Ok(new { status = "REVOKED" }),
            RevokeAgentCredentialStatus.AlreadyRevoked => Ok(new { status = "ALREADY_REVOKED" }),
            RevokeAgentCredentialStatus.NotFound => NotFound(),
            _ => throw new InvalidOperationException($"Unhandled status '{result.Status}'."),
        };
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AgentCredentialSummaryDto>>> List(
        [FromQuery] int tenantId, CancellationToken cancellationToken)
    {
        var result = await agentCredentialService.ListAsync(tenantId, cancellationToken);
        return Ok(result);
    }
}
