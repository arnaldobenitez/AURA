namespace AURA.Application.HumanLogin;

public interface IHumanLoginRepository
{
    Task<HumanLoginRecord?> FindByEmailAsync(int tenantId, string email, CancellationToken cancellationToken);
}
