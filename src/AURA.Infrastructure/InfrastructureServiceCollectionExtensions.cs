using AURA.Application.AgentCredential;
using AURA.Application.HumanLogin;
using AURA.Application.ProjectControl;
using AURA.Infrastructure.AgentCredential;
using AURA.Infrastructure.HumanLogin;
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
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddSingleton(serviceProvider =>
        {
            var options = new AgentLeaseOptions();
            configuration.GetSection(AgentLeaseOptions.SectionName).Bind(options);
            return options;
        });

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

        services.AddScoped<IAgentCredentialRepository, SqlAgentCredentialRepository>();
        services.AddScoped<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddScoped<AgentCredentialService>();

        services.AddScoped<IHumanLoginRepository, SqlHumanLoginRepository>();
        services.AddScoped<HumanLoginService>();

        return services;
    }
}
