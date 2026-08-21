using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DndCampaign.Api.Controllers;

[ApiController]
[Route("api/v1/platform")]
public sealed class PlatformController(
    HealthCheckService healthChecks,
    IWebHostEnvironment environment) : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType<PlatformStatusResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PlatformStatusResponse>> GetStatus(
        CancellationToken cancellationToken)
    {
        var database = await healthChecks.CheckHealthAsync(
            registration => registration.Name == "postgres",
            cancellationToken);
        var databaseAvailable = database.Status == HealthStatus.Healthy;

        return Ok(new PlatformStatusResponse(
            "dnd-campaign-api",
            databaseAvailable ? "operational" : "degraded",
            environment.EnvironmentName,
            typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.1.0",
            DateTimeOffset.UtcNow,
            new PlatformDependenciesResponse(
                databaseAvailable ? "connected" : "unavailable",
                "otlp")));
    }
}

public sealed record PlatformStatusResponse(
    string Service,
    string Status,
    string Environment,
    string Version,
    DateTimeOffset GeneratedAt,
    PlatformDependenciesResponse Dependencies);

public sealed record PlatformDependenciesResponse(
    string Database,
    string Telemetry);
