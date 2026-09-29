using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AURA.IntegrationTests;

public class AuraProtocolEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetAuraProtocol_ReturnsCurrentProtocolMetadata()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/aura-protocol");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("AURA_PROTOCOL", payload.GetProperty("protocolName").GetString());
        Assert.Equal("2.3", payload.GetProperty("version").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("status").GetString()));
        Assert.Equal("docs/aura/AURA_AGENT_REGISTRATION_PROTOCOL_v2.3.md", payload.GetProperty("sourceDocument").GetString());
        Assert.Equal("docs/aura/AURA_PROTOCOL_v2.2.md", payload.GetProperty("governedSourceDocument").GetString());
        Assert.True(payload.GetProperty("authenticationRequired").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("tokenEndpoint").GetString()));
        Assert.Equal("/api/project-control/projects/{projectCode}/roadmap", payload.GetProperty("roadmapEndpointTemplate").GetString());
        Assert.Equal("PUT /api/project-control/projects/{projectCode}/roadmap", payload.GetProperty("roadmapPlanEndpoint").GetString());
        Assert.Equal("/api/agent-registration/events", payload.GetProperty("registrationEventsEndpoint").GetString());
        Assert.Equal("INGESTION", payload.GetProperty("registrationCredentialActorType").GetString());
        Assert.Equal("UNVERIFIED", payload.GetProperty("registrationIdentityStatus").GetString());

        var roles = payload.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToArray();
        Assert.Contains("ANALYST", roles);
        Assert.Contains("CODING_AGENT", roles);
        Assert.Contains("AUTONOMOUS_AGENT", roles);
        Assert.Contains("INGESTION", roles);

        var modes = payload.GetProperty("modes").EnumerateArray().Select(r => r.GetString()).ToArray();
        Assert.Contains("GOVERNED_EXECUTION", modes);
        Assert.Contains("REGISTRATION_ONLY", modes);
    }

    [Fact]
    public async Task GetAuraProtocol_DoesNotRequireDatabaseConnectivity()
    {
        // Distinct WebApplicationFactory instance with no DefaultConnection configured at all -
        // this endpoint must still answer 200, unlike /api/project-control which needs SQL Server.
        await using var factoryWithoutDb = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = null,
                });
            });
        });
        var client = factoryWithoutDb.CreateClient();

        var response = await client.GetAsync("/api/aura-protocol");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
