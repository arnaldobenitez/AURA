using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AURA.Api.Admin;

/// <summary>
/// Guards the credential-administration endpoints (create/revoke/rotate/list AgentCredential)
/// with a static admin API key - deliberately not JWT, since bootstrapping the very first
/// credential can't require an already-issued token. See docs/aura/AURA_PROTOCOL_v2.1.md
/// "Administración de credenciales" for why a full admin UI/role system was not built this pass.
/// </summary>
public sealed class AdminApiKeyFilter(IConfiguration configuration, IHostEnvironment environment) : IAsyncActionFilter
{
    public const string DevelopmentOnlyPlaceholder = "DEV-ONLY-INSECURE-ADMIN-KEY-NOT-FOR-PRODUCTION";
    private const string HeaderName = "X-Admin-Api-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var configuredKey = configuration["Admin:ApiKey"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "Admin:ApiKey no está configurado. Fuera de Development debe configurarse a mano " +
                    "antes de exponer los endpoints de administración de credenciales.");
            }

            configuredKey = DevelopmentOnlyPlaceholder;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var suppliedValues) ||
            !FixedTimeEquals(suppliedValues.ToString(), configuredKey))
        {
            context.Result = new Microsoft.AspNetCore.Mvc.UnauthorizedResult();
            return;
        }

        await next();
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
