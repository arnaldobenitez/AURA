namespace AURA.Application.Registration;

public interface IRegistrationEventRepository
{
    Task<RegistrationEventOutcome> RegisterAsync(RegistrationEventCommand command, CancellationToken cancellationToken);

    Task<RegistrationEventDto?> GetAsync(int tenantId, Guid eventId, CancellationToken cancellationToken);

    Task<bool> CanCredentialRegisterForProjectAsync(int tenantId, int credentialId, string projectCode, CancellationToken cancellationToken);
}
