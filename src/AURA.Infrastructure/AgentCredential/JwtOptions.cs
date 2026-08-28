namespace AURA.Infrastructure.AgentCredential;

/// <summary>
/// JWT issuance/validation configuration. Issuer/Audience/AccessTokenLifetimeMinutes are not
/// secrets and belong in appsettings.json. SigningKey is a secret and must come only from User
/// Secrets (development) or a hand-configured production appsettings.json (same convention
/// already used for ConnectionStrings:DefaultConnection) - never committed. The single value
/// checked into appsettings.Development.json is a clearly-labeled, non-secret local-dev
/// placeholder - see AURA-PROTOCOL-V2-01's final report for why that is safe.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "aura-api";

    public string Audience { get; set; } = "aura-agents";

    public int AccessTokenLifetimeMinutes { get; set; } = 15;

    public string? SigningKey { get; set; }

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(AccessTokenLifetimeMinutes <= 0 ? 15 : AccessTokenLifetimeMinutes);
}
