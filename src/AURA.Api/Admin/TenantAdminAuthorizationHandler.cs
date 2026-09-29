using AURA.Application.AgentCredential;
using Microsoft.AspNetCore.Authorization;

namespace AURA.Api.Admin;

public sealed class TenantAdminRequirement : IAuthorizationRequirement;

public sealed class TenantAdminAuthorizationHandler(IConfiguration configuration)
    : AuthorizationHandler<TenantAdminRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, TenantAdminRequirement requirement)
    {
        var principal = context.User;
        if (principal.Identity?.IsAuthenticated != true ||
            !string.Equals(principal.FindFirst(AuraJwtClaimTypes.ActorType)?.Value, "HUMAN", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(principal.FindFirst(AuraJwtClaimTypes.AuthenticationSource)?.Value, AuraJwtClaimTypes.PortalLogin, StringComparison.Ordinal) ||
            !int.TryParse(principal.FindFirst(AuraJwtClaimTypes.TenantId)?.Value, out var tenantId) || tenantId <= 0 ||
            !int.TryParse(principal.FindFirst(AuraJwtClaimTypes.LinkedActorId)?.Value, out var humanId) || humanId <= 0)
        {
            return Task.CompletedTask;
        }

        var configuredIds = configuration.GetSection($"Admin:TenantAdminUserIds:{tenantId}").Get<string[]>() ??
            configuration[$"Admin:TenantAdminUserIds:{tenantId}"]?.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (configuredIds?.Any(value => int.TryParse(value, out var id) && id == humanId) == true)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
