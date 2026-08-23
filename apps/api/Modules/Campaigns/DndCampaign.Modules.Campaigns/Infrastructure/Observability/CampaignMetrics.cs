using System.Diagnostics;
using System.Diagnostics.Metrics;
using DndCampaign.Modules.Campaigns.Application.Ports;

namespace DndCampaign.Modules.Campaigns.Infrastructure.Observability;

internal sealed class CampaignMetrics : ICampaignMetrics
{
    public const string MeterName = "DndCampaign.Modules.Campaigns";
    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> Operations =
        Meter.CreateCounter<long>("campaigns.operations");
    private static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>("campaigns.operation.duration", "ms");

    public void OperationCompleted(string operation, string outcome, double elapsedMilliseconds)
    {
        var tags = new TagList
        {
            { "campaign.operation", operation },
            { "campaign.outcome", outcome },
        };
        Operations.Add(1, tags);
        Duration.Record(elapsedMilliseconds, tags);
    }
}
