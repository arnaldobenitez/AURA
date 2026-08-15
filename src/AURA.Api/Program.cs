using AURA.Api.Correlation;
using AURA.Api.Diagnostics;
using AURA.Infrastructure;
using AURA.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

const string DevelopmentCorsPolicy = "AuraDevelopmentCors";

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<SqlServerReadinessHealthCheck>("sql-server", tags: ["ready"]);

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

// Explicit, one-off administrative command. Never runs as part of normal startup/app.Run(),
// matching the "no automatic migration" rule already used for AuraDbContext. Invoked via
// scripts/project-control-schema-update.ps1.
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

app.MapControllers();

app.Run();

public partial class Program
{
}
