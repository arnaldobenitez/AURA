namespace AURA.Api.Contracts;

public sealed record RegistrationEventResponse(
    string Status,
    Guid? EventId,
    long? ItemId,
    string? ProjectResolution);
