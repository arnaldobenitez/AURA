using AURA.Application.ProjectControl;
using AURA.Infrastructure.Persistence;
using AURA.Infrastructure.ProjectControl;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AURA.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(configuration);

        // Resolved lazily (per DbContext instance) rather than snapshotted here, so the
        // connection string always reflects the final, fully-merged configuration.
        services.AddDbContext<AuraDbContext>((serviceProvider, options) =>
        {
            var currentConfiguration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = currentConfiguration.GetConnectionString("DefaultConnection");

            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                options.UseSqlServer(connectionString);
            }
        });

        services.AddScoped<IProjectControlRepository, SqlProjectControlRepository>();
        services.AddScoped<ProjectControlService>();
        services.AddScoped<IProjectControlSchemaMigrator, ProjectControlSchemaMigrator>();

        return services;
    }
}
