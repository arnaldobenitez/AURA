using System.Security.Claims;
using AURA.Api.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;

namespace AURA.UnitTests.Admin;

public sealed class TenantAdminAuthorizationHandlerTests
{
    [Fact]
    public async Task HandleAsync_ConfiguredHumanForTenant_Succeeds()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Admin:TenantAdminUserIds:1"] = "7, 8",
        }).Build();
        var context = CreateContext("HUMAN", 1, 8);

        await new TenantAdminAuthorizationHandler(configuration).HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_HumanNotConfigured_FailsClosed()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection([]).Build();
        var context = CreateContext("HUMAN", 1, 8);

        await new TenantAdminAuthorizationHandler(configuration).HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_AdminForDifferentTenant_Fails()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Admin:TenantAdminUserIds:2"] = "8",
        }).Build();
        var context = CreateContext("HUMAN", 1, 8);

        await new TenantAdminAuthorizationHandler(configuration).HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_HumanAgentCredentialWithoutPortalLoginSource_Fails()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Admin:TenantAdminUserIds:1"] = "8",
        }).Build();
        var context = CreateContext("HUMAN", 1, 8, portalLogin: false);

        await new TenantAdminAuthorizationHandler(configuration).HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_IngestionActor_FailsEvenWhenIdIsConfigured()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Admin:TenantAdminUserIds:1"] = "8",
        }).Build();
        var context = CreateContext("INGESTION", 1, 8);

        await new TenantAdminAuthorizationHandler(configuration).HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    private static AuthorizationHandlerContext CreateContext(string actorType, int tenantId, int humanId, bool portalLogin = true)
    {
        var claims = new List<Claim>
        {
            new("aura:actorType", actorType),
            new("aura:tenantId", tenantId.ToString()),
            new("aura:linkedActorId", humanId.ToString()),
        };
        if (portalLogin) claims.Add(new Claim("aura:authSource", "PORTAL_LOGIN"));
        var identity = new ClaimsIdentity(claims, "test");
        return new AuthorizationHandlerContext([new TenantAdminRequirement()], new ClaimsPrincipal(identity), null);
    }
}
