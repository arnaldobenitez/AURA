namespace AURA.Api.Contracts;

public sealed record HealthCheckResponse(
    string Status,
    string Service,
    string Environment,
    DateTime TimestampUtc);
