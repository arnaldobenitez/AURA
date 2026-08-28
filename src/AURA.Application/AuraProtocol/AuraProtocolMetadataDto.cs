namespace AURA.Application.AuraProtocol;

public sealed record AuraProtocolMetadataDto(
    string ProtocolName,
    string Version,
    string Status,
    DateOnly PublishedAt,
    IReadOnlyList<string> Supersedes,
    string SourceDocument,
    string AgentIntegrationDocument,
    string VersioningDocument,
    IReadOnlyList<string> Roles,
    string BoardEndpoint,
    string EventsEndpoint,
    string ProjectsEndpoint,
    string HistoryEndpointTemplate,
    // AURA_PROTOCOL v2.1 (AURA-PROTOCOL-V2-01): authentication/concurrency capability
    // description - never a secret, just where/how, so an agent can self-configure without
    // reading source. See docs/aura/AURA_PROTOCOL_v2.1.md.
    bool AuthenticationRequired,
    string TokenEndpoint,
    string LeaseModel,
    string DiscoveryNote);

/// <summary>
/// Single in-process source for AURA_PROTOCOL metadata. This is the live, queryable half of
/// "single source of truth" (see docs/aura/PROTOCOL_VERSIONING.md): the full specification
/// lives only in docs/aura/AURA_PROTOCOL_v2.1.md, never duplicated here field-by-field - this
/// type only carries the identity/version/status an agent needs to confirm it is looking at
/// the current protocol and to locate the document that actually defines it.
/// </summary>
public static class AuraProtocolMetadataProvider
{
    public static AuraProtocolMetadataDto Current { get; } = new(
        ProtocolName: "AURA_PROTOCOL",
        Version: "2.1",
        Status: "IMPLEMENTED_PENDING_PRODUCTION_VERIFICATION",
        PublishedAt: new DateOnly(2026, 8, 26),
        Supersedes: ["rules/AURA.md@2026-08-16.2", "docs/aura/AURA_PROTOCOL_v2.md@2.0"],
        SourceDocument: "docs/aura/AURA_PROTOCOL_v2.1.md",
        AgentIntegrationDocument: "docs/aura/AGENT_INTEGRATION.md",
        VersioningDocument: "docs/aura/PROTOCOL_VERSIONING.md",
        Roles: ["ANALYST", "CODING_AGENT", "AUTONOMOUS_AGENT"],
        BoardEndpoint: "/api/project-control",
        EventsEndpoint: "/api/project-control/events",
        ProjectsEndpoint: "/api/project-control/projects",
        HistoryEndpointTemplate: "/api/project-control/projects/{projectCode}/events",
        AuthenticationRequired: true,
        TokenEndpoint: "/api/auth/token",
        LeaseModel: "One lease per (tenantId, projectCode, workStream); CLAIM acquires it, " +
            "PROGRESS/HANDOFF/BLOCKER/DEPLOYMENT must present LeaseId+ExpectedLeaseVersion " +
            "(optimistic concurrency, 409 LEASE_CONFLICT on mismatch/expiry/wrong holder). " +
            "REQUEST/APPROVAL/REWORK are HUMAN governance actions and do not require it; " +
            "COMPLETION releases it.",
        DiscoveryNote:
            "This response is the live half of CURRENT_PROTOCOL discovery; " +
            "docs/aura/AURA_PROTOCOL_v2.1.md (SourceDocument) is the only full specification " +
            "and must never be duplicated elsewhere. If Version/Status here and in the " +
            "document ever disagree, both must be corrected together in the same change - " +
            "never resolve the mismatch by picking one silently. Status " +
            "IMPLEMENTED_PENDING_PRODUCTION_VERIFICATION means authentication/concurrency are " +
            "implemented and locally verified, but production activation (deployment to " +
            "https://aura.aguarapp.com plus a controlled production smoke test) has not " +
            "happened yet - do not treat this as ACTIVE, and do not distribute bootstraps " +
            "until an Analyst records that verification and flips this to ACTIVE.");
}
