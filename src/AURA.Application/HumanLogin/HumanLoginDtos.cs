namespace AURA.Application.HumanLogin;

/// <summary>
/// Basic web login (2026-08-28): a human enters email+password to view the dashboard. Tenant is
/// hardcoded to 1 for now (this is a quick access gate, not a multi-tenant login system) - see
/// the request that asked for this ("smok poner tenant 1").
/// </summary>
public sealed record HumanLoginRequest(string Email, string Password);

public enum HumanLoginStatus
{
    Success,
    InvalidCredentials,
}

public sealed record HumanLoginResult(
    HumanLoginStatus Status,
    string? AccessToken,
    DateTime? ExpiresAtUtc,
    string? UserName);

public sealed record HumanLoginRecord(
    int TenantId,
    int UserId,
    string Name,
    byte[] PasswordSalt,
    byte[] PasswordHash,
    int PasswordIterations);
