namespace AURA.Application.AgentCredential;

/// <summary>
/// Issues short-lived JWT access tokens for an already-authenticated credential. Implemented in
/// AURA.Infrastructure (needs the signing key from configuration); kept as an Application-layer
/// abstraction so AgentCredentialService never depends on ASP.NET/JWT libraries directly.
/// </summary>
public interface IAccessTokenIssuer
{
    (string AccessToken, DateTime ExpiresAtUtc) Issue(AuthenticatedActor actor);
}

/// <summary>
/// Claim type names shared between the token issuer (AURA.Infrastructure) and whatever reads
/// HttpContext.User (AURA.Api) - kept in Application so both sides reference the same constants
/// instead of duplicating string literals.
/// </summary>
public static class AuraJwtClaimTypes
{
    public const string TenantId = "aura:tenantId";
    public const string CredentialId = "aura:credentialId";
    public const string ActorType = "aura:actorType";
    public const string LinkedActorId = "aura:linkedActorId";
    public const string AgentName = "aura:agentName";
    public const string AuthenticationSource = "aura:authSource";
    public const string PortalLogin = "PORTAL_LOGIN";
}
