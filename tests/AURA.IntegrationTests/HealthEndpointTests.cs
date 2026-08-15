using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AURA.IntegrationTests;

public class HealthEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetHealth_ReturnsOkWithExpectedContract()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("AURA.Api", payload.GetProperty("service").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("status").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("environment").GetString()));

        var timestampText = payload.GetProperty("timestampUtc").GetString();
        Assert.False(string.IsNullOrWhiteSpace(timestampText));
        Assert.True(DateTime.TryParse(timestampText, out _));
    }

    [Fact]
    public async Task GetHealth_EchoesIncomingCorrelationIdHeader()
    {
        var client = factory.CreateClient();
        var correlationId = Guid.NewGuid().ToString();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-Id", correlationId);

        var response = await client.SendAsync(request);

        Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var values));
        Assert.Equal(correlationId, Assert.Single(values));
    }
}
