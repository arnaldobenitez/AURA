namespace AURA.Application.ProjectControl;

/// <summary>
/// Configuration for the CONTROL lease AURA_PROTOCOL v2.1 introduces (see
/// docs/aura/AURA_PROTOCOL_v2.1.md). Bound from configuration key "AgentLease:DurationMinutes";
/// not a secret, safe to keep in appsettings.json.
/// </summary>
public sealed class AgentLeaseOptions
{
    public const string SectionName = "AgentLease";

    public int DurationMinutes { get; set; } = 30;

    public TimeSpan Duration => TimeSpan.FromMinutes(DurationMinutes <= 0 ? 30 : DurationMinutes);
}
