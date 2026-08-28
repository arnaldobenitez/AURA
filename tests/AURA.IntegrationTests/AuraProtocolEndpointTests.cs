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
        Assert.Equal("2.1", payload.GetProperty("version").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("status").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("sourceDocument").GetString()));
        Assert.True(payload.GetProperty("authenticationRequired").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("tokenEndpoint").GetString()));

        var roles = payload.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ToArray();
        Assert.Contains("ANALYST", roles);
        Assert.Contains("CODING_AGENT", roles);
        Assert.Contains("AUTONOMOUS_AGENT", roles);
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
