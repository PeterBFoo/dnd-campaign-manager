using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DndCampaign.Modules.Access.Infrastructure.Persistence;

internal sealed class AccessPostgresHealthCheck(AccessDbContext database) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await database.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("PostgreSQL is accepting connections.")
                : HealthCheckResult.Unhealthy("PostgreSQL rejected the connection.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL health check failed.", exception);
        }
    }
}
