using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AURA.Infrastructure.Persistence;

public sealed class AuraDbContextFactory : IDesignTimeDbContextFactory<AuraDbContext>
{
    public const string ConnectionStringEnvironmentVariable = "AURA_DESIGNTIME_CONNSTR";

    public AuraDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"La variable de entorno '{ConnectionStringEnvironmentVariable}' no está definida. " +
                "Establecela con la connection string de desarrollo (sin versionarla) antes de ejecutar herramientas de EF Core.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<AuraDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new AuraDbContext(optionsBuilder.Options);
    }
}
