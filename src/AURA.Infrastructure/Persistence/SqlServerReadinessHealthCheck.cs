using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AURA.Infrastructure.Persistence;

public sealed class SqlServerReadinessHealthCheck(AuraDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Reading the connection string itself throws when no provider was configured
            // (i.e. no ConnectionStrings:DefaultConnection was set), so this stays inside
            // the try/catch alongside the rest of the checks below.
            if (string.IsNullOrWhiteSpace(dbContext.Database.GetConnectionString()))
            {
                return HealthCheckResult.Unhealthy("Persistence connection is not configured.");
            }

            var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);

            if (!canConnect)
            {
                return HealthCheckResult.Unhealthy("Database is not reachable.");
            }

            var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);

            return pendingMigrations.Any()
                ? HealthCheckResult.Unhealthy("Database has pending migrations.")
                : HealthCheckResult.Healthy("Database is reachable and up to date.");
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy("Database readiness check failed.");
        }
    }
}
