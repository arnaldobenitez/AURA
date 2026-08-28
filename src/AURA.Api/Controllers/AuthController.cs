using AURA.Api.Contracts;
using AURA.Application.AgentCredential;
using AURA.Application.HumanLogin;
using Microsoft.AspNetCore.Mvc;

namespace AURA.Api.Controllers;

/// <summary>
/// Exchanges a long-lived AgentCredential (id + secret) for a short-lived JWT access token.
/// Anonymous by construction (an agent has no token yet when it calls this) - the credential
/// itself, validated server-side, is what authenticates the caller. See
/// docs/aura/AURA_PROTOCOL_v2.1.md "Autenticación de agentes".
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(AgentCredentialService agentCredentialService, HumanLoginService humanLoginService) : ControllerBase
{
    [HttpPost("token")]
    public async Task<ActionResult<AccessTokenResponse>> IssueToken(
        [FromBody] IssueAccessTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await agentCredentialService.IssueAccessTokenAsync(request, cancellationToken);

        return result.Status switch
        {
            IssueAccessTokenStatus.Issued => Ok(new AccessTokenResponse(result.AccessToken!, result.ExpiresAtUtc!.Value)),
            IssueAccessTokenStatus.Revoked => Problem(
                title: "Credencial revocada.",
                detail: "Esta credencial fue revocada y ya no puede emitir tokens.",
                statusCode: StatusCodes.Status401Unauthorized),
            IssueAccessTokenStatus.InvalidCredential => Problem(
                title: "Credencial inválida.",
                detail: "tenantId, credentialId o secret incorrectos.",
                statusCode: StatusCodes.Status401Unauthorized),
            _ => throw new InvalidOperationException($"Unhandled status '{result.Status}'."),
        };
    }

    /// <summary>
    /// Basic web login (2026-08-28): email + password, tenant hardcoded to 1. Only gates
    /// viewing the dashboard - no in-app permissions are enforced beyond "a valid login
    /// exists" for now, by explicit request.
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequestBody request, CancellationToken cancellationToken)
    {
        var result = await humanLoginService.LoginAsync(new HumanLoginRequest(request.Email, request.Password), cancellationToken);

        return result.Status switch
        {
            HumanLoginStatus.Success => Ok(new LoginResponse(result.AccessToken!, result.ExpiresAtUtc!.Value, result.UserName!)),
            HumanLoginStatus.InvalidCredentials => Problem(
                title: "Credenciales inválidas.",
                detail: "Email o contraseña incorrectos.",
                statusCode: StatusCodes.Status401Unauthorized),
            _ => throw new InvalidOperationException($"Unhandled status '{result.Status}'."),
        };
    }
}
