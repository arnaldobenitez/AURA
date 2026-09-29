using System.Net;
using System.Net.Http.Headers;
using AURA.Application.AgentCredential;
using AURA.Domain.ProjectControl;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AURA.IntegrationTests;

public sealed class AdminAuthorizationEndpointTests
{
    [Fact]
    public async Task AdminAccess_IsAuthenticatedTenantScopedAndPortalLoginOnly()
    {
        await using var factory = new WebApplicationFactory<global::Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Admin:TenantAdminUserIds:1"] = "42",
                }));
        });
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/admin/authorization")).StatusCode);

        var nonAdminToken = IssueToken(factory, ProjectActorType.Human, 43, AuraJwtClaimTypes.PortalLogin);
        Assert.Equal(HttpStatusCode.Forbidden, await GetAdminAccess(client, nonAdminToken));

        var technicalHumanToken = IssueToken(factory, ProjectActorType.Human, 42, null);
        Assert.Equal(HttpStatusCode.Forbidden, await GetAdminAccess(client, technicalHumanToken));

        var tenantAdminToken = IssueToken(factory, ProjectActorType.Human, 42, AuraJwtClaimTypes.PortalLogin);
        Assert.Equal(HttpStatusCode.OK, await GetAdminAccess(client, tenantAdminToken));

        using var crossTenant = new HttpRequestMessage(HttpMethod.Get, "/api/admin/projects?tenantId=2");
        crossTenant.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tenantAdminToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(crossTenant)).StatusCode);

        using var sensitiveName = new HttpRequestMessage(HttpMethod.Post, "/api/admin/agent-credentials")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new
            {
                tenantId = 1,
                agentName = "password=not-a-real-secret",
                actorType = "INGESTION",
                projectIds = new[] { 1 },
            }),
        };
        sensitiveName.Headers.Add("X-Admin-Api-Key", AURA.Api.Admin.AdminApiKeyFilter.DevelopmentOnlyPlaceholder);
        var rejectedSecret = await client.SendAsync(sensitiveName);
        Assert.Equal(HttpStatusCode.BadRequest, rejectedSecret.StatusCode);
        var errorBody = await rejectedSecret.Content.ReadAsStringAsync();
        Assert.DoesNotContain("not-a-real-secret", errorBody, StringComparison.Ordinal);
    }

    private static string IssueToken(WebApplicationFactory<global::Program> factory, ProjectActorType actorType, int humanId, string? source)
    {
        using var scope = factory.Services.CreateScope();
        var issuer = scope.ServiceProvider.GetRequiredService<IAccessTokenIssuer>();
        return issuer.Issue(new AuthenticatedActor(1, humanId, actorType, humanId, "admin-auth-test", source)).AccessToken;
    }

    private static async Task<HttpStatusCode> GetAdminAccess(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/authorization");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (await client.SendAsync(request)).StatusCode;
    }
}
