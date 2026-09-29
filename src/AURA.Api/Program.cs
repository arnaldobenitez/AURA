using AURA.Api.Correlation;
using AURA.Api.Diagnostics;
using AURA.Infrastructure;
using AURA.Infrastructure.AgentCredential;
using Microsoft.AspNetCore.Authorization;
using AURA.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

const string DevelopmentCorsPolicy = "AuraDevelopmentCors";

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<SqlServerReadinessHealthCheck>("sql-server", tags: ["ready"]);

// AURA_PROTOCOL v2.1 (AURA-PROTOCOL-V2-01): autenticacion individual de agentes via JWT de
// corta duracion, emitido por POST /api/auth/token a partir de una credencial (ver
// AURA.Application.AgentCredential). Nunca se acepta localhost/desarrollo como fallback
// productivo: fuera de Development, Jwt:SigningKey debe estar configurado explicitamente (igual
// convencion que ConnectionStrings:DefaultConnection) o la API no arranca.
// No secreto real vive aca: fuera de Development, la ausencia de Jwt:SigningKey en
// configuracion (User Secrets en desarrollo "real", appsettings.json editado a mano en
// produccion - mismo mecanismo que ConnectionStrings:DefaultConnection) hace fallar el arranque.
// Dentro de Development (incluye WebApplicationFactory de tests), y solo ahi, se usa un
// placeholder fijo para no exigirle a cada test/entorno local su propio secreto - nunca
// alcanzable fuera de Development.
const string DevelopmentOnlySigningKeyPlaceholder = "DEV-ONLY-INSECURE-PLACEHOLDER-KEY-NOT-FOR-PRODUCTION-32-BYTES-MIN";
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(jwtSigningKey))
{
    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Jwt:SigningKey no esta configurado. Fuera de Development debe configurarse a mano " +
            "(mismo mecanismo que ConnectionStrings:DefaultConnection) antes de arrancar la API.");
    }

    jwtSigningKey = DevelopmentOnlySigningKeyPlaceholder;
    // Tambien se escribe en configuracion para que AgentCredentialService/JwtAccessTokenIssuer
    // (que leen Jwt:SigningKey via IOptions<JwtOptions>, no esta variable local) usen el mismo
    // valor al emitir tokens.
    builder.Configuration["Jwt:SigningKey"] = jwtSigningKey;
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        var keyBytes = JwtAccessTokenIssuer.ResolveSigningKeyBytes(jwtSigningKey);

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AuraTenantAdmin", policy =>
        policy.RequireAuthenticatedUser().AddRequirements(new AURA.Api.Admin.TenantAdminRequirement()));
});
builder.Services.AddScoped<IAuthorizationHandler, AURA.Api.Admin.TenantAdminAuthorizationHandler>();
builder.Services.AddScoped<AURA.Api.Admin.AdminApiKeyFilter>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenApi();

    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? [];

    builder.Services.AddCors(options =>
    {
        options.AddPolicy(DevelopmentCorsPolicy, policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });
}

var app = builder.Build();

// Explicit, one-off administrative commands. Never run as part of normal startup/app.Run().
if (args.Contains("--apply-project-control-schema-only"))
{
    using var scope = app.Services.CreateScope();
    var migrator = scope.ServiceProvider.GetRequiredService<AURA.Infrastructure.ProjectControl.IProjectControlSchemaMigrator>();
    await migrator.ApplySchemaOnlyAsync(CancellationToken.None);
    return;
}

if (args.Contains("--apply-project-control-schema"))
{
    using var scope = app.Services.CreateScope();
    var migrator = scope.ServiceProvider.GetRequiredService<AURA.Infrastructure.ProjectControl.IProjectControlSchemaMigrator>();
    await migrator.ApplyAsync(CancellationToken.None);
    return;
}

app.UseExceptionHandler();

app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(DevelopmentCorsPolicy);
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program
{
}
