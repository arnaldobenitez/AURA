using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AURA.IntegrationTests;

public class HealthLivenessReadinessTests
{
    [Fact]
    public async Task Live_WithoutPersistenceConfigured_ReturnsOk()
    {
        using var factory = CreateFactoryWithConnectionString(connectionString: null);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_WithoutPersistenceConfigured_ReturnsServiceUnavailableAndNoSecrets()
    {
        using var factory = CreateFactoryWithConnectionString(connectionString: null);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Server=", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("User Id", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Startup_WithUnreachableDatabaseConfigured_DoesNotAutoMigrateOrHang()
    {
        // Non-routable address with a short timeout: if the API tried to
        // migrate/connect during startup (instead of lazily, per request),
        // host creation itself would hang or throw here.
        using var factory = CreateFactoryWithConnectionString(
            "Server=169.254.10.10,1;Database=AURA;User Id=sa;Password=unreachable;" +
            "TrustServerCertificate=True;Encrypt=False;Connect Timeout=1;");

        var stopwatch = Stopwatch.StartNew();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/health/ready");
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(15),
            "El host tardó demasiado en responder; podría estar intentando migrar automáticamente al iniciar.");
    }

    private static WebApplicationFactory<Program> CreateFactoryWithConnectionString(string? connectionString)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configurationBuilder) =>
            {
                // Overrides whatever ambient User Secrets/environment the machine running
                // the tests may have, so these tests are deterministic everywhere.
                configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connectionString ?? string.Empty,
                });
            });
        });
    }
}
