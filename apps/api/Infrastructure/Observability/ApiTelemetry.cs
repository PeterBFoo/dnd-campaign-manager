using System.Diagnostics.Metrics;

namespace DndCampaign.Api.Infrastructure.Observability;

internal static class ApiTelemetry
{
    public const string MeterName = "DndCampaign.Api";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> PlatformStatusRequests = Meter.CreateCounter<long>(
        name: "dnd.platform.status.requests",
        unit: "{request}",
        description: "Number of platform status requests.");
}
