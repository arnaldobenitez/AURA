using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.UserSecrets;

namespace AURA.IntegrationTests.ProjectControl;

/// <summary>
/// Resolves the same dev connection string the app itself uses (env var, else the
/// AURA.Api User Secrets store) - never a value hardcoded in source.
/// </summary>
internal static class ProjectControlTestSupport
{
    public static string? TryGetConnectionString()
    {
        var fromEnv = Environment.GetEnvironmentVariable("AURA_DESIGNTIME_CONNSTR");
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return fromEnv;
        }

        var userSecretsId = typeof(global::Program).Assembly
            .GetCustomAttribute<UserSecretsIdAttribute>()?.UserSecretsId;

        if (userSecretsId is null)
        {
            return null;
        }

        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(userSecretsId)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        return string.IsNullOrWhiteSpace(connectionString) ? null : connectionString;
    }
}
