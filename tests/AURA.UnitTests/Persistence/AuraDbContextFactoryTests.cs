using AURA.Infrastructure.Persistence;

namespace AURA.UnitTests.Persistence;

public class AuraDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_WithoutDesignTimeConnectionString_ThrowsWithoutFallback()
    {
        var original = Environment.GetEnvironmentVariable(AuraDbContextFactory.ConnectionStringEnvironmentVariable);
        Environment.SetEnvironmentVariable(AuraDbContextFactory.ConnectionStringEnvironmentVariable, null);

        try
        {
            var factory = new AuraDbContextFactory();

            var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext([]));

            Assert.Contains(
                AuraDbContextFactory.ConnectionStringEnvironmentVariable,
                exception.Message,
                StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(AuraDbContextFactory.ConnectionStringEnvironmentVariable, original);
        }
    }
}
