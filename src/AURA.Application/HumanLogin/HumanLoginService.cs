using System.Text;
using AURA.Application.AgentCredential;
using AURA.Application.Security;
using AURA.Domain.ProjectControl;

namespace AURA.Application.HumanLogin;

/// <summary>
/// Basic web login: email+password against Usuarios, tenant hardcoded to 1. Issues the same
/// kind of JWT AgentCredential tokens use (ActorType=Human, LinkedActorId=IdUsuario) via the
/// same IAccessTokenIssuer - a human's browser session and a HUMAN AgentCredential are both
/// just "a HUMAN-typed bearer token" to everything downstream. No roles/permissions inside the
/// app yet by design (see the request this was built for) - a valid login only gates viewing.
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
            record.TenantId, record.UserId, ProjectActorType.Human, record.UserId, record.Name));

        return new HumanLoginResult(HumanLoginStatus.Success, accessToken, expiresAtUtc, record.Name);
    }
}
