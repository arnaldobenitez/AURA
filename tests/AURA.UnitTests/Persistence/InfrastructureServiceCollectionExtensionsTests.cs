using AURA.Infrastructure;
using AURA.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AURA.UnitTests.Persistence;

public class InfrastructureServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInfrastructure_WithoutConnectionString_LeavesDbContextWithoutAProvider()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuraDbContext>();

        // EF throws when asked anything of a DbContext with no provider configured;
        // that failure mode is exactly what proves persistence was left unconfigured.
        Assert.Throws<InvalidOperationException>(() => dbContext.Database.GetConnectionString());
    }

    [Fact]
    public void AddInfrastructure_WithConnectionString_ConfiguresDbContextWithASqlServerProvider()
    {
        var services = new ServiceCollection();
        const string connectionString = "Server=(local);Database=AURA;Trusted_Connection=True;";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
            })
            .Build();

        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuraDbContext>();

        // UseSqlServer normalizes the string (e.g. Server -> Data Source), so this checks
        // the effective provider configuration rather than a byte-for-byte round trip.
        var effectiveConnectionString = dbContext.Database.GetConnectionString();
        Assert.False(string.IsNullOrWhiteSpace(effectiveConnectionString));
        Assert.Contains("AURA", effectiveConnectionString, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", dbContext.Database.ProviderName);
    }
}
