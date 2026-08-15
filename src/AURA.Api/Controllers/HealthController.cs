using AURA.Api.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AURA.Api.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(
    HealthCheckService healthCheckService,
    IHostEnvironment environment) : ControllerBase
{
    [HttpGet]
    [HttpGet("live")]
    public Task<ActionResult<HealthCheckResponse>> GetLive(CancellationToken cancellationToken) =>
        Evaluate(check => check.Tags.Contains("live"), cancellationToken);

    [HttpGet("ready")]
    public Task<ActionResult<HealthCheckResponse>> GetReady(CancellationToken cancellationToken) =>
        Evaluate(check => check.Tags.Contains("ready"), cancellationToken);

    private async Task<ActionResult<HealthCheckResponse>> Evaluate(
        Func<HealthCheckRegistration, bool> predicate,
        CancellationToken cancellationToken)
    {
        var report = await healthCheckService.CheckHealthAsync(predicate, cancellationToken);

        var response = new HealthCheckResponse(
            report.Status.ToString(),
            "AURA.Api",
            environment.EnvironmentName,
            DateTime.UtcNow);

        return report.Status == HealthStatus.Unhealthy
            ? StatusCode(StatusCodes.Status503ServiceUnavailable, response)
            : Ok(response);
    }
}
