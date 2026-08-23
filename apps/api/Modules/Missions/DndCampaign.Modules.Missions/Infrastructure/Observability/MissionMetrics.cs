using System.Diagnostics;
using System.Diagnostics.Metrics;
using DndCampaign.Modules.Missions.Application.Ports;

namespace DndCampaign.Modules.Missions.Infrastructure.Observability;

internal sealed class MissionMetrics : IMissionMetrics
{
    public const string MeterName = "DndCampaign.Modules.Missions";
    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> Operations = Meter.CreateCounter<long>("missions.operations");
    private static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>("missions.operation.duration", "ms");

    public void OperationCompleted(string operation, string outcome, double elapsedMilliseconds)
    {
        var tags = new TagList
        {
            { "missions.operation", operation },
            { "missions.outcome", outcome },
        };
        Operations.Add(1, tags);
        Duration.Record(elapsedMilliseconds, tags);
    }
}
