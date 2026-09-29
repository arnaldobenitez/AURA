using System.Text;
using AURA.Application.AgentCredential;
using AURA.Application.Security;
using AURA.Domain.ProjectControl;

namespace AURA.Application.HumanLogin;

/// <summary>
/// Basic web login: email+password against Usuarios, tenant hardcoded to 1. Issues a HUMAN JWT
/// through the shared token issuer and marks its authentication source so the admin portal can
/// distinguish this login from a technical HUMAN AgentCredential. Tenant-admin authorization is
/// configured separately; a valid login alone grants no administrative permission.
/// </summary>
public sealed class HumanLoginService(IHumanLoginRepository repository, IAccessTokenIssuer tokenIssuer)
{
    public const int FixedTenantId = 1;

    public async Task<HumanLoginResult> LoginAsync(HumanLoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email?.Trim();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(request.Password))
        {
            return new HumanLoginResult(HumanLoginStatus.InvalidCredentials, null, null, null);
        }

        var record = await repository.FindByEmailAsync(FixedTenantId, email, cancellationToken);
        if (record is null)
        {
            return new HumanLoginResult(HumanLoginStatus.InvalidCredentials, null, null, null);
        }

        var passwordBytes = Encoding.UTF8.GetBytes(request.Password);
        if (!Pbkdf2PasswordHasher.Verify(passwordBytes, record.PasswordSalt, record.PasswordHash, record.PasswordIterations))
        {
            return new HumanLoginResult(HumanLoginStatus.InvalidCredentials, null, null, null);
        }

        var (accessToken, expiresAtUtc) = tokenIssuer.Issue(new AuthenticatedActor(
            record.TenantId, record.UserId, ProjectActorType.Human, record.UserId, record.Name, AuraJwtClaimTypes.PortalLogin));

        return new HumanLoginResult(HumanLoginStatus.Success, accessToken, expiresAtUtc, record.Name);
    }
}
