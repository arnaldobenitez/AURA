using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AURA.Api.Admin;

/// <summary>
/// Protects administrative endpoints with a configured tenant-admin human JWT for the portal,
/// while retaining the static admin API key solely for backend compatibility/break-glass calls.
/// The API key is never intended for browser use.
/// </summary>
public sealed class AdminApiKeyFilter(
    IConfiguration configuration,
    IHostEnvironment environment,
    IAuthorizationService authorizationService) : IAsyncActionFilter
{
    public const string DevelopmentOnlyPlaceholder = "DEV-ONLY-INSECURE-ADMIN-KEY-NOT-FOR-PRODUCTION";
    private const string HeaderName = "X-Admin-Api-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var isAuthenticated = context.HttpContext.User.Identity?.IsAuthenticated == true;
        if (isAuthenticated && (await authorizationService.AuthorizeAsync(context.HttpContext.User, null, "AuraTenantAdmin")).Succeeded)
        {
            await next();
            return;
        }

        var configuredKey = configuration["Admin:ApiKey"];
        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            if (!environment.IsDevelopment())
            {
                context.Result = isAuthenticated
                    ? new Microsoft.AspNetCore.Mvc.ForbidResult()
                    : new Microsoft.AspNetCore.Mvc.UnauthorizedResult();
                return;
            }

            configuredKey = DevelopmentOnlyPlaceholder;
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var suppliedValues) ||
            !FixedTimeEquals(suppliedValues.ToString(), configuredKey))
        {
            context.Result = isAuthenticated
                ? new Microsoft.AspNetCore.Mvc.ForbidResult()
                : new Microsoft.AspNetCore.Mvc.UnauthorizedResult();
            return;
        }

        await next();
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
