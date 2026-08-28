using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AURA.Application.AgentCredential;
using AURA.Application.ProjectControl;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AURA.Infrastructure.AgentCredential;

public sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options) : IAccessTokenIssuer
{
    public (string AccessToken, DateTime ExpiresAtUtc) Issue(AuthenticatedActor actor)
    {
        var jwtOptions = options.Value;
        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey no está configurado. En desarrollo, cargalo con User Secrets " +
                "(o el placeholder no-secreto de appsettings.Development.json); en producción " +
                "debe configurarse a mano en el appsettings.json desplegado, igual que " +
                "ConnectionStrings:DefaultConnection.");
        }

        var expiresAtUtc = DateTime.UtcNow.Add(jwtOptions.AccessTokenLifetime);

        var claims = new List<Claim>
        {
            new(AuraJwtClaimTypes.TenantId, actor.TenantId.ToString()),
            new(AuraJwtClaimTypes.CredentialId, actor.CredentialId.ToString()),
            new(AuraJwtClaimTypes.ActorType, ProjectControlCodes.ToCode(actor.ActorType)),
            new(AuraJwtClaimTypes.AgentName, actor.AgentName),
        };

        if (actor.LinkedActorId is { } linkedActorId)
        {
            claims.Add(new Claim(AuraJwtClaimTypes.LinkedActorId, linkedActorId.ToString()));
        }

        var signingKeyBytes = ResolveSigningKeyBytes(jwtOptions.SigningKey);
        var credentials = new SigningCredentials(new SymmetricSecurityKey(signingKeyBytes), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtOptions.Issuer,
            audience: jwtOptions.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }

    /// <summary>
    /// Accepts either a base64-encoded key (preferred - fixed, adequate entropy for HMAC-SHA256)
    /// or a plain UTF-8 passphrase (fallback, for a hand-typed value); tries base64 first.
    /// </summary>
    public static byte[] ResolveSigningKeyBytes(string signingKey)
    {
        try
        {
            var decoded = Convert.FromBase64String(signingKey);
            if (decoded.Length >= 32)
            {
                return decoded;
            }
        }
        catch (FormatException)
        {
            // No era base64 valido - se usa como passphrase UTF-8 mas abajo.
        }

        return Encoding.UTF8.GetBytes(signingKey);
    }
}
