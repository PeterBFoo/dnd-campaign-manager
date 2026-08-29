using System.Diagnostics;
using System.Diagnostics.Metrics;
using DndCampaign.Modules.AdventureCatalog.Application.Ports;

namespace DndCampaign.Modules.AdventureCatalog.Infrastructure.Observability;

internal sealed class AdventureCatalogMetrics : IAdventureCatalogMetrics
{
    public const string MeterName = "DndCampaign.Modules.AdventureCatalog";
    private static readonly Meter Meter = new(MeterName, "1.0.0");
    private static readonly Counter<long> Operations =
        Meter.CreateCounter<long>("adventure_catalog.operations");
    private static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>("adventure_catalog.operation.duration", "ms");

    public void OperationCompleted(string operation, string outcome, double elapsedMilliseconds)
    {
        var tags = new TagList
        {
            { "adventure_catalog.operation", operation },
            { "adventure_catalog.outcome", outcome },
        };
        Operations.Add(1, tags);
        Duration.Record(elapsedMilliseconds, tags);
    }
}
