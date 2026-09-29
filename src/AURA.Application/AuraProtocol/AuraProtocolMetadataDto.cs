namespace AURA.Application.AuraProtocol;

public sealed record AuraProtocolMetadataDto(
    string ProtocolName,
    string Version,
    string Status,
    DateOnly PublishedAt,
    IReadOnlyList<string> Supersedes,
    string SourceDocument,
    string GovernedSourceDocument,
    string AgentIntegrationDocument,
    string VersioningDocument,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Modes,
    string BoardEndpoint,
    string EventsEndpoint,
    string ProjectsEndpoint,
    string HistoryEndpointTemplate,
    string RoadmapEndpointTemplate,
    string RoadmapPlanEndpoint,
    string RegistrationEventsEndpoint,
    string RegistrationReadEndpointTemplate,
    IReadOnlyList<string> RegistrationEventTypes,
    string RegistrationCredentialActorType,
    string RegistrationIdentityStatus,
    // AURA_PROTOCOL v2.1 (AURA-PROTOCOL-V2-01): authentication/concurrency capability
    // description - never a secret, just where/how, so an agent can self-configure without
    // reading source. See docs/aura/AURA_PROTOCOL_v2.2.md and
    // docs/aura/AURA_AGENT_REGISTRATION_PROTOCOL_v2.3.md.
    bool AuthenticationRequired,
    string TokenEndpoint,
    string LeaseModel,
    string DiscoveryNote);

/// <summary>
/// Single in-process source for AURA_PROTOCOL metadata. This is the live, queryable half of
/// "single source of truth" (see docs/aura/PROTOCOL_VERSIONING.md): the governed execution
/// specification remains docs/aura/AURA_PROTOCOL_v2.2.md, while v2.3 adds the registration-only
/// contract in docs/aura/AURA_AGENT_REGISTRATION_PROTOCOL_v2.3.md. This DTO only carries the
/// identity/version/status and endpoint locations an agent needs to discover both modes.
/// </summary>
public static class AuraProtocolMetadataProvider
{
    public static AuraProtocolMetadataDto Current { get; } = new(
        ProtocolName: "AURA_PROTOCOL",
        Version: "2.3",
        Status: "IMPLEMENTED_PENDING_PRODUCTION_VERIFICATION",
        PublishedAt: new DateOnly(2026, 9, 27),
        Supersedes: ["rules/AURA.md@2026-08-16.2", "docs/aura/AURA_PROTOCOL_v2.md@2.0", "docs/aura/AURA_PROTOCOL_v2.1.md@2.1"],
        SourceDocument: "docs/aura/AURA_AGENT_REGISTRATION_PROTOCOL_v2.3.md",
        GovernedSourceDocument: "docs/aura/AURA_PROTOCOL_v2.2.md",
        AgentIntegrationDocument: "docs/aura/AGENT_INTEGRATION.md",
        VersioningDocument: "docs/aura/PROTOCOL_VERSIONING.md",
        Roles: ["ANALYST", "CODING_AGENT", "AUTONOMOUS_AGENT", "INGESTION"],
        Modes: ["GOVERNED_EXECUTION", "REGISTRATION_ONLY"],
        BoardEndpoint: "/api/project-control",
        EventsEndpoint: "/api/project-control/events",
        ProjectsEndpoint: "/api/project-control/projects",
        HistoryEndpointTemplate: "/api/project-control/projects/{projectCode}/events",
        RoadmapEndpointTemplate: "/api/project-control/projects/{projectCode}/roadmap",
        RoadmapPlanEndpoint: "PUT /api/project-control/projects/{projectCode}/roadmap",
        RegistrationEventsEndpoint: "/api/agent-registration/events",
        RegistrationReadEndpointTemplate: "/api/agent-registration/events/{eventId}?tenantId={tenantId}",
        RegistrationEventTypes: ["INFO", "PROGRESS", "DECISION", "BLOCKER", "HANDOFF", "REQUEST", "NOTE", "ERROR"],
        RegistrationCredentialActorType: "INGESTION",
        RegistrationIdentityStatus: "UNVERIFIED",
        AuthenticationRequired: true,
        TokenEndpoint: "/api/auth/token",
        LeaseModel: "One lease per (tenantId, projectCode, workStream); CLAIM acquires it, " +
            "PROGRESS/HANDOFF/BLOCKER/DEPLOYMENT must present LeaseId+ExpectedLeaseVersion " +
            "(optimistic concurrency, 409 LEASE_CONFLICT on mismatch/expiry/wrong holder). " +
            "REQUEST and legacy APPROVAL are HUMAN governance actions. Roadmap APPROVAL uses " +
            "the assigned AI_ANALYST credential and does not require the work lease; COMPLETION releases it. " +
            "REGISTRATION_ONLY uses INGESTION credentials and never touches governed leases. New ingestion credentials are project-scoped and revalidated on each write/read-back; legacy tenant-wide credentials are transition-only.",
        DiscoveryNote:
            "This response is the live half of CURRENT_PROTOCOL discovery. " +
            "docs/aura/AURA_PROTOCOL_v2.2.md remains normative for GOVERNED_EXECUTION and " +
            "docs/aura/AURA_AGENT_REGISTRATION_PROTOCOL_v2.3.md is the additive source for " +
            "REGISTRATION_ONLY. If Version/Status here and in either document ever disagree, " +
            "correct both together in the same change - never resolve the mismatch silently. " +
            "Status IMPLEMENTED_PENDING_PRODUCTION_VERIFICATION means implementation is locally " +
            "verified but production activation and controlled read-back are still pending; do not " +
            "treat this as ACTIVE or distribute bootstraps until the Analyst records verification.");
}
